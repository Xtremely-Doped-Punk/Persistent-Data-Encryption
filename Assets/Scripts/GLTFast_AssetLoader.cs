using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace GLTFast
{
    using Loading;
    using Logging;
    using Materials;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine.Rendering;

    public class GLTFast_AssetLoader : GltfAssetBase
    {
        public static GLTFast_AssetLoader Instance;
        private void Awake()
        {
            if (Instance != null)
                Destroy(gameObject);
            else
                Instance = this;
        }
        /// <summary>
        /// Scene to load (-1 loads glTFs default scene)
        /// </summary>
        protected int SceneId => sceneId;

        /// <summary>
        /// If true, the first animation clip starts playing right after instantiation.
        /// </summary>
        public bool PlayAutomatically => playAutomatically;

        /// <inheritdoc cref="GLTFast.InstantiationSettings"/>
        public InstantiationSettings InstantiationSettings
        {
            get => instantiationSettings;
            set => instantiationSettings = value;
        }

        [SerializeField]
        [Tooltip("Override scene to load (-1 loads glTFs default scene)")]
        int sceneId = -1;
        
        [SerializeField]
        [Tooltip("If true, the first animation clip starts playing right after instantiation")]
        bool playAutomatically = true;
        
        [SerializeField]
        InstantiationSettings instantiationSettings;

        [SerializeField]
        Transform gameObjectWrapper;
        /// <summary>
        /// Latest scene's instance.
        /// </summary>
        public GameObjectSceneInstance SceneInstance { get; protected set; }
        
        /// <inheritdoc />
        public override async Task<bool> Load(
            string gltfUrl,
            IDownloadProvider downloadProvider = null,
            IDeferAgent deferAgent = null,
            IMaterialGenerator materialGenerator = null,
            ICodeLogger logger = null
            )
        {
            logger = logger ?? new ConsoleLogger();
            var success = await base.Load(gltfUrl, downloadProvider, deferAgent, materialGenerator, logger);
            if (success)
            {
                if (deferAgent != null) await deferAgent.BreakPoint();
                // Auto-Instantiate
                if (sceneId >= 0)
                {
                    success = await InstantiateScene(sceneId, logger);
                }
                else
                {
                    success = await Instantiate(logger);
                }
            }
            return success;
        }
        
        CancellationTokenSource currentCancellationTokenSource;
        public bool cancelCurrentImport(string uri)
        {
            if (currentUrl == uri && gltfImport != null)
            {
                currentCancellationTokenSource.Cancel();
                Debug.Log("Cancellation :: Canceling current import");
                var loadedAsset = gameObjectWrapper.GetChild(0);
                if (loadedAsset != null)
                {
                    if (loadedAsset.GetComponent<GLTFast_AssetUnloader>() == null)
                    {
                        Debug.LogError("Cancellation :: There is a case where the loaded object is destroyed on cancellation without GLTFast_AssetUnloader component in it");
                    }
                    else
                    {
                        Debug.Log("Cancellation :: Destroy Object on Cancellation");
                        DestroyImmediate(loadedAsset.gameObject);
                    }
                }
                //var objects = GetUnityObjectsFromGLTFImport(gltfImport);
                //if(objects.Count>0)
                //{
                //    foreach(var item in objects)
                //}
                gltfImport.Dispose();
                currentUrl = "";
                gltfImport = null;
            
            }
            else
                Debug.Log("Cancellation:: Nothing is there to cancel");
            return true;
        }
        string currentUrl = "";
        GltfImport gltfImport;

        /// <summary>
        /// Load a glTF file (JSON or binary) using URL string
        /// that can be a file path (using the "file://" scheme) or a web address.
        /// </summary>
        public async Task<(bool, Transform)> LoadAssetFromUrl(string gltfUrl, ICodeLogger logger = null)
        {
            // First step: load glTF
            gltfImport = new GLTFast.GltfImport();
            var success = await gltfImport.Load(gltfUrl, ImportSettings);
            return await LoadAsset(logger, success);
        }

        /// <summary>
        /// Load a glTF from a byte array 
        /// that can either be of (JSON or glTF-Binary).
        /// </summary>
        public async Task<(bool, Transform)> LoadAssetFromBinary(byte[] gltfData, ICodeLogger logger = null)
        {
            // First step: load glTF
            gltfImport = new GLTFast.GltfImport();
            var success = await gltfImport.Load(gltfData, importSettings: ImportSettings);
            Transform loadedAsset = null;

            currentCancellationTokenSource = new CancellationTokenSource();
            var token = currentCancellationTokenSource.Token;

            var instantiator = new GameObjectInstantiator(gltfImport, gameObjectWrapper, logger, instantiationSettings);
            if (success)
            {
                // Here you can customize the post-loading behavior

                // Get the first material
                var material = gltfImport.GetMaterial();
                Debug.LogFormat("The first material is called {0}", material.name);

                await gltfImport.InstantiateMainSceneAsync(instantiator, token);
                loadedAsset = gameObjectWrapper.GetChild(0);
                if (loadedAsset != null)
                {
                    var objects = GetUnityObjectsFromGLTFImport(gltfImport);
                    loadedAsset.gameObject.AddComponent<GLTFast_AssetUnloader>().Init(objects);
                }
            }
            else
            {
                loadedAsset = null;
                Debug.LogError("Loading glTF failed!");
            }
            return (success, loadedAsset);
        }

        private async Task<(bool, Transform)> LoadAsset(ICodeLogger logger, bool success)
        {
            Transform loadedAsset = null;

            currentCancellationTokenSource = new CancellationTokenSource();
            var token = currentCancellationTokenSource.Token;

            var instantiator = new GameObjectInstantiator(gltfImport, gameObjectWrapper, logger, instantiationSettings);
            if (success)
            {
                // Here you can customize the post-loading behavior

                // Get the first material
                var material = gltfImport.GetMaterial();
                Debug.LogFormat("The first material is called {0}", material.name);

                await gltfImport.InstantiateMainSceneAsync(instantiator, token);
                loadedAsset = gameObjectWrapper.GetChild(0);
                if (loadedAsset != null)
                {
                    var objects = GetUnityObjectsFromGLTFImport(gltfImport);
                    loadedAsset.gameObject.AddComponent<GLTFast_AssetUnloader>().Init(objects);
                }
            }
            else
            {
                loadedAsset = null;
                Debug.LogError("Loading glTF failed!");
            }
            return (success, loadedAsset);
        }

        List<UnityEngine.Object> GetUnityObjectsFromGLTFImport(GltfImport _gltfImport)
        {
            var animclips = _gltfImport.GetAnimationClips();
            var meshes = _gltfImport.GetMeshes();
            List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            if (meshes != null)
                objects.AddRange(meshes);
            if (animclips != null)
                objects.AddRange(animclips);
            if (_gltfImport.TextureCount > 0)
            {
                for (int i = 0; i < _gltfImport.TextureCount; i++)
                {
                    objects.Add(_gltfImport.GetTexture(i));
                }
            }
            if (_gltfImport.MaterialCount > 0)
            {
                for (int i = 0; i < _gltfImport.MaterialCount; i++)
                {
                    var mat = _gltfImport.GetMaterial(i);
                    mat.SetFloat("_Cull", (float)CullMode.Off);
                    objects.Add(mat);
                }
            }
            return objects;
        }
        
        /// <inheritdoc />
        protected override IInstantiator GetDefaultInstantiator(ICodeLogger logger)
        {
            return new GameObjectInstantiator(Importer, gameObjectWrapper, logger, instantiationSettings);
        }
        
        /// <inheritdoc />
        protected override void PostInstantiation(IInstantiator instantiator, bool success)
        {
            SceneInstance = (instantiator as GameObjectInstantiator)?.SceneInstance;
#if UNITY_ANIMATION
            if (SceneInstance != null) {
                if (playAutomatically) {
                    var legacyAnimation = SceneInstance.LegacyAnimation;
                    if (legacyAnimation != null) {
                        SceneInstance.LegacyAnimation.Play();
                    }
                }
            }
#endif
            base.PostInstantiation(instantiator, success);
        }
        
        /// <inheritdoc />
        public override void ClearScenes()
        {
            //foreach (Transform child in transform)
            //{
            //    Destroy(child.gameObject);
            //}
            //SceneInstance = null;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{ 
    public class GLTFast_AssetUnloader : MonoBehaviour
    {
        public void Init(List<Object> objects)
        {
            Allocations = objects;
        }
        /// <summary>
        /// Assets Allocation List.
        /// </summary>
        public List<Object> Allocations = new List<Object>();
        private void OnDestroy()
        {
            if (Allocations.Count <= 0)
                return;
            for (var i = 0; i < Allocations.Count; i++)
            {
                var allocation = Allocations[i];
                if (allocation == null)
                {
                    continue;
                }

                Destroy(allocation);
            }
        }
    }
}
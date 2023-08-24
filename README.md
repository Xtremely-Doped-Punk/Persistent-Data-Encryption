# Data Persistence in Unity
Learn how to save and load data in Unity - avoiding common mistakes and dangerous serialization techniques many other tutorials will tell you to use!

In this repository we'll look at using text-based serialization using JSON as well as binary-based file read/write (load glb asssets dynamically) with optionally encrypt that data. you can learn about some of the pros/cons for using text-based serialization, and what you should use instead if you really want to/need to use a binary serialization technique.

Common suggestions that you should absolutely not use to persist game state data are:

1. Player Prefs - these are not designed for storing game state. Only...Player Preferences such as graphic & audio settings.
2. BinaryFormatter - this class is dangerous and insecure. Use of this class can allow an attacker to take over the system. https://docs.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide 

## Requirements
* Requires Unity 2021.3 LTS or higher.

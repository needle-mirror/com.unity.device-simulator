using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal class DeviceSimulatorSettings : ScriptableObject
    {
        [SerializeField] public bool SystemInfoDefaultAssembly;
        [SerializeField] public string[] SystemInfoAssemblies;
    }
}

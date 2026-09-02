namespace Threadlight.Mirroring.ExtensionExample
{
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LiveMirroringExtensionExampleSettings : MonoBehaviour
{
    public bool requireScaleReference = true;
    public string previewNameSuffix = " - Example";
}
}

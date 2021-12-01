using System;
using UnityEngine;

namespace UnityEditor.Rendering.Universal
{
    [Obsolete("ForwardRendererDataEditor has been deprecated. Use UniversalRendererDataEditor instead (UnityUpgradable) -> UniversalRendererDataEditor", true)]
    public class ForwardRendererDataEditor : ScriptableRendererDataEditor
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            throw new NotSupportedException("ForwardRendererDataEditor has been deprecated. Use UniversalRendererDataEditor instead");
        }
    }

    static partial class EditorUtils
    {
    }
}

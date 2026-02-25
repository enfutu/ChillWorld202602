using UnityEditor;
using UnityEngine;

namespace GPUInfiniteGrass {
    
    [CustomPropertyDrawer(typeof(LayerAttribute))]
    public sealed class LayerDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType != SerializedPropertyType.Integer) {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            property.intValue = EditorGUI.LayerField(position, label, property.intValue);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight;
        }
    }

}

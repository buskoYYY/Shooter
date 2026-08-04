
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Lightbug.Utilities
{
    public enum HelpBoxMessageType
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class HelpBoxAttribute : PropertyAttribute
    {
        public string Text;
        public HelpBoxMessageType MessageType;

        public HelpBoxAttribute(string text, HelpBoxMessageType messageType)
        {
            Text = text;
            MessageType = messageType;
        }
    }

#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(HelpBoxAttribute))]
    public class HelpBoxAttributeEditor : DecoratorDrawer
    {
        const float VERTICAL_PADDING = 6f;

        HelpBoxAttribute att => (HelpBoxAttribute)attribute;

        public override float GetHeight()
        {
            string msg = string.IsNullOrEmpty(att.Text) ? " " : att.Text;
            int lineCount = msg.Split('\n').Length;
            return EditorGUIUtility.singleLineHeight * lineCount + 2f * VERTICAL_PADDING;
        }

        public override void OnGUI(Rect position)
        {
            EditorGUI.HelpBox(position, att.Text, (MessageType)att.MessageType);
        }

    }

#endif

}



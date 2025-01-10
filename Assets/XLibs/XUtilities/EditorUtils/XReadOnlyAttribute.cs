using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class XReadOnlyAttribute : PropertyAttribute
{
}


#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(XReadOnlyAttribute))]
public class XReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // ����ԭ���� GUI.enabled ״̬
        bool previousGUIState = GUI.enabled;

        // ���� GUI.enabled Ϊ false ��ֹ�༭
        GUI.enabled = false;

        // ����Ĭ�ϵ������ֶ�
        EditorGUI.PropertyField(position, property, label);

        // �ָ�ԭ���� GUI.enabled ״̬
        GUI.enabled = previousGUIState;
    }

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUI.GetPropertyHeight(property, label);
	}
}

#endif
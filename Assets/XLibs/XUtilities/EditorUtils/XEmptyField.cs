// A field that's not drawn,
// used for a workaround that:
// Header's left margin becomes 0 when used with custom drawn property (drawn with PropertyDrawer.OnGUI),
// We can use Header attribute with this field to make the header margin correct.
//
// һ�����ᱻ���Ƶ��ֶΣ�
// ���ڽ��������⣺
// �����Զ�����Ƶ�����һ��ʹ��ʱ��Header ����߾���Ϊ 0���� PropertyDrawer.OnGUI ���ƣ���
// ���ǿ���ʹ�� Header ����������ֶ�һ����ʹ Header ����߾���ȷ��

using System;
using UnityEngine;
using UnityEngine.UIElements;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A field that's not drawn,
/// used for a workaround that:
/// Header's left margin becomes 0 when used with custom drawn property (drawn with PropertyDrawer.OnGUI),
/// We can use Header attribute with this field to make the header margin correct.
/// 
/// һ�����ᱻ���Ƶ��ֶΣ�
/// ���ڽ��������⣺
/// �����Զ�����Ƶ�����һ��ʹ��ʱ��Header ����߾���Ϊ 0���� PropertyDrawer.OnGUI ���ƣ���
/// ���ǿ���ʹ�� Header ����������ֶ�һ����ʹ Header ����߾���ȷ��
/// </summary>
[Serializable]
public class XEmptyField
{
}

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(XEmptyField))]
public class XEmptyFieldDrawer : PropertyDrawer
{
	public override VisualElement CreatePropertyGUI(SerializedProperty property)
	{
		// Return an empty VisualElement
		// this will make Header on the top of the property to be drawn correctly
		return new VisualElement();
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return 0;
	}
}

#endif

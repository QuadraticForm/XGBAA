using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("XConstraint/1. Constraints/Twist Correction Constraint")]
public class XTwistCorrectionConstraint : XConstraintWithSingleSourceAndSingleTarget
{
    [Header("Params")]

    public Axis axis = Axis.Y;

	// not using Space directly to limit options, discarding meaningless ones for Twist Correction 
	public enum TwistCorrectionSpace
	{
		World = Space.World,

		Parent = Space.Parent,

		LocalRest = Space.LocalRest,

		// LocalCurrent = Space.LocalCurrent, // LocalCurrent is meaningless for Twist Correction

		// TODO, add other meaningful spaces like "custom"
	}

	public TwistCorrectionSpace space = TwistCorrectionSpace.LocalRest;

    static Quaternion MaskTwist(Quaternion q, Axis axis)
    {
        if (axis == Axis.X)
            return new Quaternion(q.x, 0, 0, q.w);
        else if (axis == Axis.Y)
            return new Quaternion(0, q.y, 0, q.w);
        else if (axis == Axis.Z)
            return new Quaternion(0, 0, q.z, q.w);
        else
            return Quaternion.identity;
    }

    static public void ApplyLocalSpace(
      float influence, Transform source, Transform target, Axis axis, Quaternion sourceRest, Quaternion targetRest)
    {
		// terminology: in XConstraint, similar to blender, source means constrained object while target is depended on by source, "rest" means init state
		//
		// math:
		// there are 2 ways to understand B * A.
		// 1. extrinsic: A is done first, then B, using the global rotation tool.
		// 2. intrinsic: B is done first, then A, using the local rotation tool.
		//
		// so, when it comes to "twist", we think of it using the 2 method:
		//
		// current = init * twist, 
		// that is: twist is done using the local rotation tool.
		//
		// now, we want to apply "target"'s twist on "source", like rotating it using local rotation tool
		// 
        // ����� XConstraint ���У����� Blender��source ָ��Լ���Ķ���target ָ�������Ķ���rest ָ��ʼ״̬
        //
        // ��ѧ���ͣ�
        // �����ַ�ʽ�������Ԫ���ĳ� B * A��
		// 
		// ���� A ������ X ��ת N�㣬B ������ Y ��ת M��
        // 1. ����������ʹ�� global ��ת���ߣ���ִ�� A����ִ�� B��
        // 2. ����������ʹ�� local ��ת���ߣ���ִ�� B����ִ�� A��
        //
        // ����� twist ʱ������ʹ�õڶ��ַ�����
        //
        // current = rest * twist
        // Ҳ����˵��twist ������ʹ�� local ��ת���ߣ����Ÿ����ᣬ��תһ���ڵ㡣
        //
        // ���ڣ�����ϣ���Ѷ� target ���� twist���� source ���ظ�һ�顣
        //
        // ��������ϸ����ѧ�Ƶ���
        //
        // targetCurrent = targetRest * targetTwist
        // inverse(targetRest) * targetCurrent = inverse(targetRest) * targetRest * targetTwist
        // inverse(targetRest) * targetCurrent = targetTwist
        //
        // sourceCurrent = sourceRest * targetTwist
        
        var targetTwist = Quaternion.Inverse(targetRest) * target.localRotation;
        targetTwist = MaskTwist(targetTwist, axis);
        source.localRotation = Quaternion.Lerp(sourceRest, sourceRest * targetTwist, influence);
    }

	static public void ApplyParentSpace(
      float influence, Transform source, Transform target, Axis axis)
    {
        // math:
		// 1. match source and target's parent space rotation using a delta rotation in source's local space
		// targetCurrent = sourceCurrent * deltaSourceLocal;
		//
		// 2. calculate the delta rotation
		// Inverse(sourceCurrent) * targetCurrent = Inverse(sourceCurrent) * sourceCurrent * deltaSourceLocal;
		// Inverse(sourceCurrent) * targetCurrent = deltaSourceLocal;
		// 
		// 3. mask out rotation on axis that are not the twist axis
		// twist = MaskTwist(deltaSourceLocal, axis)
		//
		// 4. apply twist to source
		// sourceCurrent = sourceCurrent * twist

		// NOTE: objects rotation in parent space is called "localRotation", 
		// don't get confused with rotation in local space, which is rotation relative to its inital local space

		// Calculate rot: rotation needed to match target.
		var deltaSourceLocal = Quaternion.Inverse(source.localRotation) * target.localRotation;

		// Mask out other axes and isolate desired twist.
		var twist = MaskTwist(deltaSourceLocal, axis);

		// Apply this twist to source.
		var newSourceRotation = source.localRotation * twist;

		// Lerp based on influence
		source.localRotation = Quaternion.Lerp(source.localRotation, newSourceRotation, influence);
    }

    static public void ApplyWorldSpace(float influence, Transform source, Transform target, Axis axis)
    {
		// math:
		// 1. match source and target's world space rotation using a delta rotation in source's local space
		// targetCurrent = sourceCurrent * deltaSourceLocal;
		//
		// 2. calculate the delta rotation
		// Inverse(sourceCurrent) * targetCurrent = Inverse(sourceCurrent) * sourceCurrent * deltaSourceLocal;
		// Inverse(sourceCurrent) * targetCurrent = deltaSourceLocal;
		// 
		// 3. mask out rotation on axis that are not the twist axis
		// twist = MaskTwist(deltaSourceLocal, axis)
		//
		// 4. apply twist to source
		// sourceCurrent = sourceCurrent * twist

		// Calculate rot: rotation needed to match target.
		var deltaSourceLocal = Quaternion.Inverse(source.rotation) * target.rotation;

		// Mask out other axes and isolate desired twist.
		var twist = MaskTwist(deltaSourceLocal, axis);

		// Apply this twist to source.
		var newSourceRotation = source.rotation * twist;

		// Lerp based on influence
		source.rotation = Quaternion.Lerp(source.rotation, newSourceRotation, influence);
    }
    
    public override void Resolve()
	{
	    if (space == TwistCorrectionSpace.LocalRest)
		{
			ApplyLocalSpace(Influence, Source, target, axis, sourceRest.localRotation, targetRest.localRotation);
		}
		else if (space == TwistCorrectionSpace.Parent)
		{
			ApplyParentSpace(Influence, Source, target, axis);
		}
		else if (space == TwistCorrectionSpace.World)
		{
            ApplyWorldSpace(Influence, Source, target, axis);
        }
	}
}
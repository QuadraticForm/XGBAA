using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
[AddComponentMenu("XConstraint/2. Constraints (physical)/Collision")]
public class XCollisionConstraint : XConstraintWithSingleSource
{
    [Header("Params")]
    public LayerMask collisionMask;    // ��ײ���Ĳ�
    public float radius = 0.5f;        // ��ײ���İ뾶
    public Vector3 offset;             // ��ײ���ƫ�������ֲ��ռ䣩
	[Min(1)]
	public int forwardSteps = 3;      
	// public int backwardSteps = 2;

    private Vector3 prevPos;           // ��¼��һ֡��λ�ã�����ȷ�����������ײ��������
	private bool isPrevPosInited = false;

	[Header("Callback")]

	public UnityEvent onCollision;

	public override void Resolve()
	{
		if (!isPrevPosInited)
		{
			prevPos = Source.position;
			isPrevPosInited = true;
		}

		// ���ϴε�λ����������Ŀ��λ��

		var targetPos = Source.position;
		var currentPos = prevPos;

        // ƫ��Ӱ���µ�ʵ����ײ���λ��
        var targetCheckPos = Source.TransformPoint(offset);
		var currentCheckPos = currentPos + targetCheckPos - targetPos;

        // 
        var delta = targetPos - currentPos;
        var step = delta / forwardSteps;

		bool collided = false;

        for (int i = 0; i < forwardSteps; i++)
        {
			if (Physics.CheckSphere(currentCheckPos + step, radius, collisionMask))
			{
				collided = true;
				break; 
			}

			currentPos += step;
			currentCheckPos += step;
        }

		if (collided)
			onCollision.Invoke();

        // ���� Lerp ��ֵ��Ӧ�� _influence
        Source.position = Vector3.Lerp(Source.position, currentPos, Influence);

        prevPos = Source.position;
    }

	/*
    public override void Execute(float upperLayerInfluence)
    {
        var _influence = influence * upperLayerInfluence;
        var currentPos = Source.position;

        // ƫ��Ӱ���µ�ʵ����ײ���λ��
        var checkPos = Source.TransformPoint(offset);

        // ���㵱ǰ֡����һ֡��λ�ò���
        var delta = currentPos - prevPos;
        var back = -delta.normalized;
        var step = delta.magnitude / maxIterations;

		bool collided = false;

        // ��ײ�����ѭ���ϲ�
        for (int i = 0; i <= maxIterations; i++)
        {
            // ���û����ײ������ѭ��
            if (!Physics.CheckSphere(checkPos, radius, collisionMask))
            {
                break;
            }

            // ���������ײ�ˣ���ô���˹̶�����

			collided = true;

            checkPos += back * step;
            currentPos += back * step;  // ͬʱ���� currentPos
        }

		if (collided)
			onCollision.Invoke();

        // ���� Lerp ��ֵ��Ӧ�� _influence
        Source.position = Vector3.Lerp(Source.position, currentPos, _influence);

        prevPos = Source.position;
    }
	*/

    private void OnDrawGizmos()
    {
        if (Source == null) return;

        Gizmos.color = Color.red;

        // �ھֲ��ռ���Ӧ��ƫ�Ʋ�ת��������ռ�����ʾ Gizmo
        var offsetWorld = Source.TransformPoint(offset);
        Gizmos.DrawWireSphere(offsetWorld, radius);
    }
}
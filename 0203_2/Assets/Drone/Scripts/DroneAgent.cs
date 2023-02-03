using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using PA_DronePack;
//using System.Numerics;
using Vector3 = UnityEngine.Vector3;
//using Unity.VisualScripting;


public class DroneAgent : Agent
{
    private PA_DroneController dcoScript;
    public RayScript rayscript;


    public DroneSettings area;
    public GameObject goal;


    float preDist;

    private Transform agentTrans;
    private Transform goalTrans;

    private Rigidbody agent_Rigidbody;
    private RayPerceptionSensorComponent3D[] rayPerceptionSensor;
    private float restrictDistance;

    /// <summary>
    /// �ʱ�ȭ �۾��� ���� �ѹ� ȣ��Ǵ� �޼ҵ�
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        dcoScript = gameObject.GetComponent<PA_DroneController>();

        agentTrans = gameObject.transform;
        goalTrans = goal.transform;

        agentTrans.LookAt(goal.transform);  //�������ڸ��� goal���� �ٶ󺸰� ��

        agent_Rigidbody = gameObject.GetComponent<Rigidbody>();
        rayPerceptionSensor = gameObject.GetComponents<RayPerceptionSensorComponent3D>();

        MaxStep = 20000;

        Academy.Instance.AgentPreStep += WaitTimeInference;
        
        restrictDistance = Vector3.Magnitude(goalTrans.position - agentTrans.position) + 10f;
        //Debug.Log("startDistance : " + (restrictDistance - 10f));
    }

    /// <summary>
    /// ȯ�� ������ ���� �� ������ ��å ������ ���� �극�ο� �����ϴ� �޼ҵ�
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor)
    {
        //�Ÿ� ����
        sensor.AddObservation(agentTrans.position - goalTrans.position);
        //�ӵ� ����
        sensor.AddObservation(agent_Rigidbody.velocity);
        //�� �ӵ� ����
        sensor.AddObservation(agent_Rigidbody.angularVelocity);
        //�ٴ� �Ÿ�
        sensor.AddObservation(rayscript.distance);
    }

    /// <summary>
    /// RayPerceptionSensor3D�� ���� ray�� ��� ��ü�� ���� ����� �Ÿ� return
    /// </summary>
    /// <param name="rayComponent"></param>
    /// <returns></returns>
    private float[] MinRayCastDist(RayPerceptionSensorComponent3D[] rayComponent)
    {
        float[] min = { 100f, 100f };   // { staticObstacle, dynamicObstacle }
        GameObject goHit;

        for (int i = 0; i < rayComponent.Length; i++)
        {
            var rayOutputs = RayPerceptionSensor
                    .Perceive(rayComponent[i].GetRayPerceptionInput())
                    .RayOutputs;

            if (rayOutputs != null)     //raycast�� �ϳ��� ������ ���
            {
                var lengthOfRayOutputs = RayPerceptionSensor
                        .Perceive(rayComponent[i].GetRayPerceptionInput())
                        .RayOutputs
                        .Length;

                for (int j = 0; j < lengthOfRayOutputs; j++)
                {
                    goHit = rayOutputs[j].HitGameObject;

                    if (goHit != null)  //�� �ٱ� �������� ��ü�� ������ ���
                    {
                        var rayDirection = rayOutputs[j].EndPositionWorld - rayOutputs[j].StartPositionWorld;
                        var scaledRayLength = rayDirection.magnitude;
                        float rayHitDistance = rayOutputs[j].HitFraction * scaledRayLength;

                        if (goHit.CompareTag("StaticObstacle") && min[0] > rayHitDistance) min[0] = rayHitDistance;     //���� ����� ���� ��ֹ����� �Ÿ� update
                        if (goHit.CompareTag("DynamicObstacle") && min[1] > rayHitDistance) min[1] = rayHitDistance;     //���� ����� ���� ��ֹ����� �Ÿ��Ÿ� update
                        
                    }
                }
            }
        }
        return min;
    }


    /// <summary>
    /// �극��(��å)���� ���� ���� ���� �ൿ�� �����ϴ� �޼ҵ�
    /// </summary>
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        AddReward(-0.1f);

        var actions = actionBuffers.ContinuousActions;

        float moveX = Mathf.Clamp(actions[0], -1, 1f);
        float moveY = Mathf.Clamp(actions[1], -1, 1f);
        float moveZ = Mathf.Clamp(actions[2], -1, 1f);

        dcoScript.DriveInput(moveX);
        dcoScript.StrafeInput(moveY);
        dcoScript.LiftInput(moveZ);


        float distance = Vector3.Magnitude(goalTrans.position - agentTrans.position);   //target�� ��а��� �Ÿ�
        float[] ObstacleDistance = MinRayCastDist(rayPerceptionSensor);   //��ֹ����� �Ÿ��� ray perception sensor�� ������ 10���� �� �� �ּ� �Ÿ�
        float distfrombottom = rayscript.distance;  //�ٴڰ��� �Ÿ� ������

        if (distance <= 10)    //��ǥ���� ����
        {
            //Debug.Log("Goal!!!!!(" + StepCount + ")");
            //SetReward(60f);
            SetReward(600f);
            EndEpisode();
        }

        
        else if (distance > restrictDistance)    //��ǥ������ �ʹ� �־��� ��
        {
            //Debug.Log(distance);
            //Debug.Log("it's too far!!" + StepCount);
            SetReward(-600f);
            EndEpisode();
        }
        
        else if(agentTrans.localPosition.x < 0 || agentTrans.localPosition.z < 0 || agentTrans.position.y < 0)   //�Ʒ��� ���� �Ѿ ��
        {
            //Debug.Log("it's out of bound!!" + StepCount);
            SetReward(-500f);
            EndEpisode();
        }

        else if (distfrombottom > 20 || distfrombottom < 3)
        {
            SetReward(-400f);
            EndEpisode();

        }

        else if (ObstacleDistance[0] < 1f)     //������ֹ��� �ε��� ���
        {
            //Debug.Log("collapse!!" + StepCount);
            //Debug.Log(StepCount);
            SetReward(-300f);
            EndEpisode();
        }

        

        else if (ObstacleDistance[1] < 3f)  //������ֹ��� �ε��� ���
        {
            SetReward(-300f);
            EndEpisode();
        }

        else    //��ǥ������ ������ �� ��
        {
            //Debug.Log(ObstacleDistance[0]);
            float reward = preDist - distance;  //�ִ� 0.3034����


            AddReward(2*reward);
            preDist = distance;
        }
    }


    /// <summary>
    /// ���Ǽҵ�(�н�����)�� �����Ҷ����� ȣ��
    /// </summary>
    public override void OnEpisodeBegin()
    {
        area.AreaSetting();
        preDist = Vector3.Magnitude(goalTrans.position - agentTrans.position);
        rayscript.distance = 5;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.collider.CompareTag("DynamicObstacle") || collision.collider.CompareTag("StaticObstacle"))
        {
            Debug.Log("�ε���");
            SetReward(-300);
            EndEpisode();
        }
    }

    /// <summary>
    /// ������(�����)�� ���� ����� ���� �� ȣ���ϴ� �޼ҵ�(�ַ� �׽�Ʈ�뵵 or ����н��� ���)
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionOut = actionsOut.ContinuousActions;

        continuousActionOut[0] = Input.GetAxis("Vertical");
        continuousActionOut[1] = Input.GetAxis("Horizontal");
        continuousActionOut[2] = Input.GetAxis("Mouse ScrollWheel");
    }
    
    public float DecisionWatingTime = 5f;
    float m_currentTime = 0f;

    public void WaitTimeInference(int action)
    {
        if(Academy.Instance.IsCommunicatorOn)
        {
            RequestDecision();
        }

        else
        {
            if(m_currentTime >= DecisionWatingTime)
            {
                m_currentTime = 0f;
                RequestDecision();
            }

            else
            {
                m_currentTime += Time.fixedDeltaTime;
            }
        }
    }

}

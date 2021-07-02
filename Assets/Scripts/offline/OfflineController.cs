using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OfflineController : MonoBehaviour
{
    #region Variables

    [Header("Movement")] public List<AxleInfo> axleInfos;
    public float forwardMotorTorque = 100000;
    public float backwardMotorTorque = 50000;
    public float maxSteeringAngle = 15;
    public float engineBrake = 1e+12f;
    public float footBrake = 1e+24f;
    public float topSpeed = 200f;
    public float downForce = 1000f;
    public float slipLimit = 0.2f;
    private float m_SteerHelper = 0.8f;


    private float CurrentRotation { get; set; }
    private float InputAcceleration { get; set; }
    private float InputSteering { get; set; }
    private float InputBrake { get; set; }

    #region nuestras variables

    //timpo limite para la comprobacion de si el coche esta dado la vuelta o no se puede mover
    private float targetTimeFlip, targetTimePos;
    private const float MAX_TIME_FLIP_WAIT = 5.0f;
    private const float MAX_TIME_POS_WAIT = 3.0f;

    //objeto que contiene un pole position manager y componente pole position manager
    private Vector3 oldPos; //guarda una posicion anterior para comprobar si se ha movido

    private Rigidbody m_Rigidbody; //rigid body
    public List<GameObject> controlPoints;
    private List<Bounds> controlPointBounds;

    private int lastControlPoint;

    #endregion

    private float m_CurrentSpeed = 0;

    private float Speed
    {
        get { return m_CurrentSpeed; }
        set
        {
            if (Math.Abs(m_CurrentSpeed - value) < float.Epsilon) return;
            m_CurrentSpeed = value;
        }
    }


    //inputController
    private InputController _input;
    private Vector2 _movement;
    private float _jump;
    private float _escape;

    #endregion Variables

    #region Unity Callbacks

    public void Awake()
    {
        //input controller 
        _input = new InputController();
        m_Rigidbody = GetComponent<Rigidbody>();
        controlPointBounds = new List<Bounds>();
        lastControlPoint = 0;
        for (int i = 0; i < controlPoints.Count; i++)
        {
            controlPointBounds.Add(controlPoints[i].GetComponent<BoxCollider>().bounds);
        }

        /* se establecen los tiempos limites para si el coche esta girado o no se puede avanzar
         y se guarda la posicion inicial del coche para tener un punto de partida*/
        targetTimeFlip = MAX_TIME_FLIP_WAIT;
        targetTimePos = MAX_TIME_POS_WAIT;
        oldPos = transform.position;
    }

    public void Update()
    {
        //para girar el coche y devolverlo a la posicion del ultimpo punto de control superado
        TempflipCar();
        //si te quedas atasacado, apartir de 10 segundos vuleves al ultimo punto de control superado 
        tempPos();


        //controles y velocidad
        _movement = _input.Player.Movement.ReadValue<Vector2>();
        _jump = _input.Player.Jump.ReadValue<float>();
        _escape = _input.Player.escape.ReadValue<float>();

        Speed = m_Rigidbody.velocity.magnitude;
    }


    public void FixedUpdate()
    {
        if (_escape != 0)
        {
            SceneManager.LoadScene(0);
        }

        InputAcceleration = _movement.y;
        InputSteering = _movement.x;
        InputBrake = _jump;

        InputSteering = Mathf.Clamp(InputSteering, -1, 1);
        InputAcceleration = Mathf.Clamp(InputAcceleration, -1, 1);
        InputBrake = Mathf.Clamp(InputBrake, 0, 1);

        float steering = maxSteeringAngle * InputSteering;

        foreach (AxleInfo axleInfo in axleInfos)
        {
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }

            if (axleInfo.motor)
            {
                if (InputAcceleration > float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = forwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = forwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (InputAcceleration < -float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (Math.Abs(InputAcceleration) < float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = 0;
                    axleInfo.leftWheel.brakeTorque = engineBrake;
                    axleInfo.rightWheel.motorTorque = 0;
                    axleInfo.rightWheel.brakeTorque = engineBrake;
                }

                if (InputBrake > 0)
                {
                    axleInfo.leftWheel.brakeTorque = footBrake;
                    axleInfo.rightWheel.brakeTorque = footBrake;
                }
            }

            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }

        SteerHelper();
        SpeedLimiter();
        AddDownForce();
        TractionControl();
    }

    #endregion

    #region Methods

    #region control del coche

    // crude traction control that reduces the power to wheel if the car is wheel spinning too much
    private void TractionControl()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit wheelHitLeft;
            WheelHit wheelHitRight;
            axleInfo.leftWheel.GetGroundHit(out wheelHitLeft);
            axleInfo.rightWheel.GetGroundHit(out wheelHitRight);

            if (wheelHitLeft.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitLeft.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.leftWheel.motorTorque -= axleInfo.leftWheel.motorTorque * howMuchSlip * slipLimit;
            }

            if (wheelHitRight.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitRight.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.rightWheel.motorTorque -= axleInfo.rightWheel.motorTorque * howMuchSlip * slipLimit;
            }
        }
    }

    // this is used to add more grip in relation to speed
    private void AddDownForce()
    {
        foreach (var axleInfo in axleInfos)
        {
            axleInfo.leftWheel.attachedRigidbody.AddForce(
                -transform.up * (downForce * axleInfo.leftWheel.attachedRigidbody.velocity.magnitude));
        }
    }

    private void SpeedLimiter()
    {
        float speed = m_Rigidbody.velocity.magnitude;
        if (speed > topSpeed)
            m_Rigidbody.velocity = topSpeed * m_Rigidbody.velocity.normalized;
    }

    /* finds the corresponding visual wheel
     correctly applies the transform*/
    public void ApplyLocalPositionToVisuals(WheelCollider col)
    {
        if (col.transform.childCount == 0)
        {
            return;
        }

        Transform visualWheel = col.transform.GetChild(0);
        Vector3 position;
        Quaternion rotation;
        col.GetWorldPose(out position, out rotation);
        var myTransform = visualWheel.transform;
        myTransform.position = position;
        myTransform.rotation = rotation;
    }

    private void SteerHelper()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit[] wheelHit = new WheelHit[2];
            axleInfo.leftWheel.GetGroundHit(out wheelHit[0]);
            axleInfo.rightWheel.GetGroundHit(out wheelHit[1]);
            foreach (var wh in wheelHit)
            {
                if (wh.normal == Vector3.zero)
                    return; // wheels arent on the ground so dont realign the rigidbody velocity
            }
        }

        // this if is needed to avoid gimbal lock problems that will make the car suddenly shift direction
        if (Mathf.Abs(CurrentRotation - transform.eulerAngles.y) < 10f)
        {
            var turnAdjust = (transform.eulerAngles.y - CurrentRotation) * m_SteerHelper;
            Quaternion velRotation = Quaternion.AngleAxis(turnAdjust, Vector3.up);
            m_Rigidbody.velocity = velRotation * m_Rigidbody.velocity;
        }

        CurrentRotation = transform.eulerAngles.y;
    }

    #endregion

    private void OnEnable()
    {
        _input.Enable();
    }

    private void OnDisable()
    {
        _input.Disable();
    }

    /*comprueba la colision del rigid body con los objetos puntos de control para asi actualizar el ultimo punto
    de control superado y poder llevar la cuenta de las vueltas*/
    private void ManageControlPoints()
    {
        int aux = lastControlPoint; //ultimo punto de control actual
        int nextAux = (aux + 1) % controlPointBounds.Count; //punto de control siguiente

        //si el coche supera el suiguiente punto de control, actualiza en el servidor el ultimo punto de control superado
        //y en caso de pasar por linea de meta, se añade una vuelta
        if (controlPointBounds[nextAux].Contains(m_Rigidbody.position))
        {
            lastControlPoint = nextAux;
        }
    }


    /*si el transform del rigidbody esta dado la vuelta, se actualiza un contador de 5 segundos para dar tiempo por si
     pudiera darse la vuelta*/
    private void TempflipCar()
    {
        if (m_Rigidbody.transform.up.y < 0) TemporizadorFlip();
    }

    /* controla si el coche esta parado, o no puede avanzar, por lo tanto si pasado 10 segundos no ha cambiado su posicion
     se transportará al ultimo punto de control superado*/
    private void tempPos()
    {
        TemporizadorPos();
    }

    /*resta tiempo del timer para el giro y en caso de terminar el tiemer, vuelve al ultimo punto de control */
    public void TemporizadorFlip()
    {
        targetTimeFlip -= Time.deltaTime;

        //cuando termina el tiempo se cambia la escena
        if (targetTimeFlip < 0)
        {
            m_Rigidbody.transform.position = controlPointBounds[lastControlPoint].center;
            m_Rigidbody.transform.up = Vector3.up;
            targetTimeFlip = MAX_TIME_FLIP_WAIT;
        }

        //si no se estanca se resetea el tiempo
        if (m_Rigidbody.transform.up.y > 0) targetTimeFlip = MAX_TIME_FLIP_WAIT;
    }

    /*si se termina el tiempo y la posicion no ha cambiado respecto a la ultima posicion, se vuelve al ultimo punto de
     control superado*/
    public void TemporizadorPos()
    {
        targetTimePos -= Time.deltaTime;
        float auxDist = (transform.position - oldPos).magnitude;
        //cuando termina el tiempo se termina se comprueba las posiciones
        if (targetTimePos < 0)
        {
            if (auxDist < 0.1f && InputAcceleration != 0)
            {
                m_Rigidbody.transform.position = controlPointBounds[lastControlPoint].center;
                m_Rigidbody.transform.up = Vector3.up;
            }

            oldPos = transform.position;
            targetTimePos = MAX_TIME_POS_WAIT;
        }

        //si ha cambiado, se actualiza tanto la posicion como el timer
        if (auxDist > 0.1f)
        {
            oldPos = transform.position;
            targetTimePos = MAX_TIME_POS_WAIT;
        }
    }

    #endregion
}
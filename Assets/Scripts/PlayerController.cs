using System;
using System.Collections.Generic;
using System.Timers;
using UnityEngine;
using Mirror;
using UnityEditor;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class PlayerController : NetworkBehaviour
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
    public TimerSuma tiempo;

    //objeto que contiene un pole position manager y componente pole position manager
    private GameObject pole;
    private PolePositionManager _positionManager;

    private Vector3 oldPos; //guarda una posicion anterior para comprobar si se ha movido

    private PlayerInfo m_PlayerInfo; //informacion del jugador
    private Rigidbody m_Rigidbody; //rigid body

    //diccionarios para sincronizar el las vueltas y el ultimo punto de control superado por el cliente
    public SyncDictionary<int, int> lastPositions = new SyncDictionary<int, int>();
    public SyncDictionary<int, int> lastLap = new SyncDictionary<int, int>();
    [SyncVar] private int numLaps;

    #endregion

    private float m_CurrentSpeed = 0;

    private float Speed
    {
        get { return m_CurrentSpeed; }
        set
        {
            if (Math.Abs(m_CurrentSpeed - value) < float.Epsilon) return;
            m_CurrentSpeed = value;
            if (OnSpeedChangeEvent != null)
                OnSpeedChangeEvent(m_CurrentSpeed);
        }
    }

    //delegados
    public delegate void OnSpeedChangeDelegate(float newVal); //delegado para el cambio de velocidad 

    public delegate void OnLapChangeDelegate(int newVal); //delegado para el cambio de vuelta

    public delegate void OnTimeChangeDelegate(float tiempo, float tiempoTotal); //delegado para el cambio de tiempo

    public delegate void OnVictoryDelegate(float time); //delegado para el cambio de tiempo


    //eventos
    public event OnSpeedChangeDelegate OnSpeedChangeEvent; //evento actualizar velocidad
    public event OnLapChangeDelegate OnLapChangeEvent; //evento actualizar vuelta
    public event OnTimeChangeDelegate OnTimeChangeEvent; //evento actualizar timepo
    public event OnVictoryDelegate OnVictoryEvent; //evento actualizar victoria

    //inputController
    private InputController _input;
    private Vector2 _movement;
    private float _jump;

    #endregion Variables

    #region Unity Callbacks

    public void Awake()
    {
        //input controller 
        _input = new InputController();

        //obtencion de componentes de la escena
        m_Rigidbody = GetComponent<Rigidbody>();
        m_PlayerInfo = GetComponent<PlayerInfo>();
        pole = GameObject.FindGameObjectWithTag("pole");
        _positionManager = pole.GetComponent<PolePositionManager>();

        /*se añade en el servidor la informacion sobre el ulltimo punto de control superado
        y la vuelta actual*/
        if (isServer)
        {
            foreach (PlayerInfo info in _positionManager.GetPlayers())
            {
                lastPositions.Add(info.ID, info.lastControlPoint);
                lastLap.Add(info.ID, info.CurrentLap);
            }
        }

        /* se establecen los tiempos limites para si el coche esta girado o no se puede avanzar
         y se guarda la posicion inicial del coche para tener un punto de partida*/
        numLaps = 3;
        targetTimeFlip = MAX_TIME_FLIP_WAIT;
        targetTimePos = MAX_TIME_POS_WAIT;
        oldPos = transform.position;
    }

    public void Update()
    {
        /*cuando el servidor da la orden desde poleposition manager de empezar, se desbloquean los controles y 
        comienza a correr el tiempo y por tanto la carrera*/
        if (_positionManager.raceStarts)
        {
            //comienza a correr el tiempo cuando los jugadores estan listos
            tiempo.StartTimer();
            Debug.Log(message: "Tiempo transcurrido: " + tiempo.timeElapsed);

            //para girar el coche y devolverlo a la posicion del ultimpo punto de control superado
            TempflipCar();
            //si te quedas atasacado, apartir de 10 segundos vuleves al ultimo punto de control superado 
            tempPos();
            //actualizar el ultimo punto de control superado
            ManageControlPoints();

            if (m_PlayerInfo.CurrentPosition != -1)
            {
                OnVictoryEvent(tiempo.timeElapsed);
                tiempo.Pause();
            }else if ( _positionManager.numPlayers > 1)
            {
                  OnTimeChangeEvent(tiempo.timeElapsed, tiempo.currentLapTime);
            }

            if (_positionManager.numPlayers == 1)
            {
                m_PlayerInfo.timeTotal = tiempo.timeElapsed;
                OnVictoryEvent(tiempo.timeElapsed);
                tiempo.Pause();
            }

            //controles y velocidad


            _movement = _input.Player.Movement.ReadValue<Vector2>();
            _jump = _input.Player.Jump.ReadValue<float>();

            Speed = m_Rigidbody.velocity.magnitude;
        }
    }

    public void FixedUpdate()
    {
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
        int aux = m_PlayerInfo.lastControlPoint; //ultimo punto de control actual
        int nextAux = (aux + 1) % m_PlayerInfo.controlPoints.Count; //punto de control siguiente
        int nextAux2 = (aux + 2) % m_PlayerInfo.controlPoints.Count; //punto de control siguiente al siguiente
        int lastAux = aux - 1 < 0 ? m_PlayerInfo.controlPoints.Count - 1 : aux - 1; //punto de control anterior         

        //si el coche supera el suiguiente punto de control, actualiza en el servidor el ultimo punto de control superado
        //y en caso de pasar por linea de meta, se añade una vuelta
        if (m_PlayerInfo.controlPoints[nextAux].Contains(m_Rigidbody.position))
        {
            CmdUpdateControlPoints(m_PlayerInfo.ID, nextAux, m_PlayerInfo.CurrentLap, m_Rigidbody.position,
                tiempo.timeElapsed);
        }

        //si el coche atraviesa el punto de control anterior o cualquiera diferente al siguiente
        //se transporta el coche a la psocion del ultimo punto de control
        if (m_PlayerInfo.controlPoints[lastAux].Contains(m_Rigidbody.position) ||
            m_PlayerInfo.controlPoints[nextAux2].Contains(m_Rigidbody.position))
        {
            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.transform.position = m_PlayerInfo.controlPoints[aux].center;
            m_Rigidbody.transform.up = Vector3.up;
        }

        /*cuando se pasa por la linea de meta, se actualiza por pantalla el numero de vultas y se resetea
         el tiempo de la ultima vuelta*/
        if (m_PlayerInfo.lastControlPoint == 0)
        {
            if (m_PlayerInfo.CurrentLap == numLaps) //numero de vueltas
            {
                Debug.Log("aaaaaaaaaaaaaa tiempo de " + m_PlayerInfo.Name + tiempo.timeElapsed);
                m_PlayerInfo.timeTotal = tiempo.timeElapsed;
                OnVictoryEvent(tiempo.timeElapsed);
                tiempo.Pause();
            }

            OnLapChangeEvent(m_PlayerInfo.CurrentLap);
            if (tiempo.lastLap != m_PlayerInfo.CurrentLap)
            {
                tiempo.lastLap = m_PlayerInfo.CurrentLap;
                tiempo.resetLapTime();
            }
        }
    }


    /*el cliente ejecuta en el servidor los puntos de control y en caso pasar por el punto de control 0 que corresponde
     con la meta se suma una vuelta*/
    [Command]
    public void CmdUpdateControlPoints(int id, int nextPos, int lap, Vector3 position, float time)
    {
        if (m_PlayerInfo.controlPoints[nextPos].Contains(position))
        {
            lastPositions.Remove(id); //se quita el ultimo punto de control
            lastLap.Remove(id); //se quita la ultima vuelta      

            if (nextPos == 0)
            {
                lap++;
            }

            lastPositions.Add(id, nextPos); //se pone un valor actual del ultimo punto de control
            lastLap.Add(id, lap); //se pone un valor actual de la ultima vuelta
        }

        RpcUpdateControlPoints();
    }


    /*se actualiza en el cliente los valores del servidor de las vueltas y el ultimo punto de control*/
    [ClientRpc]
    public void RpcUpdateControlPoints()
    {
        int auxNewPos, auxNewLap;
        lastPositions.TryGetValue(m_PlayerInfo.ID, out auxNewPos);
        m_PlayerInfo.lastControlPoint = auxNewPos;
        lastLap.TryGetValue(m_PlayerInfo.ID, out auxNewLap);
        m_PlayerInfo.CurrentLap = auxNewLap;
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
            m_Rigidbody.transform.position = m_PlayerInfo.controlPoints[m_PlayerInfo.lastControlPoint].center;
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
                m_Rigidbody.transform.position = m_PlayerInfo.controlPoints[m_PlayerInfo.lastControlPoint].center;
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
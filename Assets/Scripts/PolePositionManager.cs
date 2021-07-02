using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

public class PolePositionManager : NetworkBehaviour
{
    #region variables

    //variables sincronizadas 
    [SyncVar] public int jugadoresTerminado;
    [SyncVar] public int numPlayers = 0; //numero actual de jugaddores
    [SyncVar] public int maxPlayers; //numero maximo de jugadores
    [SyncVar] public bool raceStarts; // indica que los jugadores pueden empezar para que empiecen todos a la vez

    //un string con las posiciones para actualizar las posiciones por pantalla y en la pantalla de vistoria         
    [SyncVar] public string raceOrder, raceOrderWithTime;
    private MyNetworkManager _networkManager;

    //se asigna por el editor los objetos que corresponde con los puntos de control en los cueles se encuantran los box colliders 
    //con los bounds para detectar si esta dentro o no
    [SerializeField] private List<GameObject> puntosControl;
    private List<Bounds> puntosControlBounds;

    //lista con los playerinfo de los clientes actializados 
    private readonly List<PlayerInfo> _players = new List<PlayerInfo>(4);
    private CircuitController _circuitController;
    private GameObject[] _debuggingSpheres;

    //delegado y evento que lanzan el evento a la UI de actualizar el la posicion
    public delegate void OnPositionChangeDelegate(string newVal, string newVal2); //delegado para las posiciones

    public event OnPositionChangeDelegate OnPositionChangeEvent; //evento actualizar las posiciones

    #endregion


    private void Awake()
    {
        if (_networkManager == null) _networkManager = FindObjectOfType<MyNetworkManager>();
        if (_circuitController == null) _circuitController = FindObjectOfType<CircuitController>();

        _debuggingSpheres = new GameObject[_networkManager.maxConnections];
        for (int i = 0; i < _networkManager.maxConnections; ++i)
        {
            _debuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _debuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
            _debuggingSpheres[i].GetComponent<MeshRenderer>().enabled = false;
        }

        //obtencion de los puntos de control de la escena
        puntosControlBounds = new List<Bounds>();
        for (int i = 0; i < puntosControl.Count; i++)
        {
            puntosControlBounds.Add(puntosControl[i].GetComponent<BoxCollider>().bounds);
        }

        //inicializacion de las variables
        jugadoresTerminado = 0;
        raceOrder = "";
        raceOrderWithTime = "";
        raceStarts = false;
        maxPlayers = 5;
    }

    private void Update()
    {
        /*el servidor ejecuta este fragmento de codigo para comprobar si se ha desconectado del servidor, y en caso de
         haberse desconectado se quita de la lista de jugadores*/
        if (isServer)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] == null)
                {
                    RpcDeleteGonePlayers(_players[i].ID);
                    _players.Remove(_players[i]);
                    numPlayers--;
                }
                else
                {
                    Debug.Log(_players[i].name);
                }
            }
        }

        /*el servidor comprueba si se han conectado todos los jugadores*/
        if (isServer && !raceStarts)
        {
            CheckRaceStart();
        }


        if (_players.Count == 0)
            return;

        /*el proceso de la carrera solo lo actualiza el servidor*/
        if (isServer)
        {
            UpdateRaceProgress();
        }

        /*se lanza el evento para actualizar por pantalla las posiciones, el distinto de null es pos si ocurriera algun fallo*/
        if (OnPositionChangeEvent != null) OnPositionChangeEvent(raceOrder, raceOrderWithTime);
        //Debug.Log("El orden de carrera es: " + raceOrder);
    }

    /*cunado un jugador se desconecta el sercidor borra de todos los clientes a ese jugador*/
    [ClientRpc]
    public void RpcDeleteGonePlayers(int ID)
    {
        var oldInfo = _players.Find(x => x.ID == ID);
        _players.Remove(oldInfo);
    }

    /*el servidor comprueba si el numero de jugadores el el maximo establecido y empiza en caso correcto*/
    [Server]
    public void CheckRaceStart()
    {
        if (numPlayers >= maxPlayers) raceStarts = true;
    }

    /*el servidor actualiza el maximo de jugadores por partida*/
    [Server]
    public void UpdateMaxPlayers(int number)
    {
        maxPlayers = number;
    }

    /*devuelve la lista de jugadores que tiene el cliente*/
    public List<PlayerInfo> GetPlayers()
    {
        return _players;
    }

    /*añade un jgador e inicializa sus variables*/
    public void AddPlayer(PlayerInfo player)
    {
        player.controlPoints = puntosControlBounds;
        player.CurrentPosition = -1;
        player.lastControlPoint = puntosControlBounds.Count - 1;
        player.CurrentLap = -1;
        _players.Add(player);

        //borra los jugadores duplicados que haya en la lista
        for (int i = 0; i < _players.Count; i++)
        {
            for (int j = i + 1; j < _players.Count; j++)
            {
                if (_players[i].ID == _players[j].ID)
                {
                    _players.Remove(_players[j]);
                }
            }
        }
    }

    /*el servidor actualiza los tiempos en todos los jugadores*/

    #region Update time

    [Server]
    public void UpdateTime(int ID, float newtime)
    {
        var oldInfo = _players.Find(x => x.ID == ID);
        oldInfo.timeTotal = newtime;
        if (oldInfo.CurrentPosition == -1)
        {
            oldInfo.CurrentPosition = jugadoresTerminado;
            jugadoresTerminado++;
        }

        RpcUpdateTimeClientPlayers(oldInfo.ID, oldInfo.timeTotal, oldInfo.CurrentPosition);
    }

    [ClientRpc]
    public void RpcUpdateTimeClientPlayers(int ID, float newTime, int posicion)
    {
        var oldInfo = _players.Find(x => x.ID == ID);
        oldInfo.timeTotal = newTime;
        oldInfo.CurrentPosition = posicion;
    }

    #endregion

    /*actualiza el nombre de un jugador */

    #region update player

    [Server]
    public void UpdatePlayer(int ID, string newName)
    {
        var oldInfo = _players.Find(x => x.ID == ID);
        oldInfo.Name = newName;
        RpcUpdateClientPlayers(oldInfo.ID, oldInfo.Name);
    }

    [ClientRpc]
    public void RpcUpdateClientPlayers(int ID, string newName)
    {
        //se asigna a old info la referencia al jugador en el cliente
        var oldInfo = _players.Find(x => x.ID == ID);
        oldInfo.Name = newName;
    }

    #endregion

    /*se ordenan las posiciones de carrera en el el servidor en las variables raceorder que son syncvars*/
    [Server]
    public void UpdateRaceProgress()
    {
        for (int i = 0; i < _players.Count; i++)
        {
            _players[i].CurrentProgress = ComputeCarArcLength(i);
        }

        //se ordena la lista (en base al IEComparable del playerInfo)
        _players.Sort();

        //diccionario auxiliar para guardar las posiciones de los jugadores que ya han terminado
        Dictionary<int, PlayerInfo> playersFinished = new Dictionary<int, PlayerInfo>();
        foreach (PlayerInfo player in _players)
        {
            //si no han terminado su valor es -1, en caso de haber terminado, su valor es distinto
            if (player.CurrentPosition != -1)
            {
                playersFinished.Add(player.CurrentPosition, player);
            }
        }

        /*se recorre la lista y se guarda en los strings el orden de carrera*/

        int j = 1;
        int k = 1;
        string myRaceOrder = "";
        string myRaceOrderWithTime = "";

        //orden de carrera con tiempo
        for (int i = 0; i < _players.Count; i++)
        {
            PlayerInfo auxPlayer;

            if (playersFinished.TryGetValue(i, out auxPlayer))
            {
                float minutes = Mathf.FloorToInt(auxPlayer.timeTotal / 60);
                float seconds = Mathf.FloorToInt(auxPlayer.timeTotal % 60);
                float milis = Mathf.FloorToInt((auxPlayer.timeTotal * 100) % 100);

                string timeText = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, milis);
                myRaceOrderWithTime += k + "º " + auxPlayer.Name + " tiempo: " + timeText + "\n";
                k++;
            }
        }

        //orden de carrera sin tiempo
        for (int i = _players.Count - 1; i >= 0; i--)
        {
            myRaceOrder += j + "º " + _players[i].Name + "\n";
            j++;
        }

        //se guardan los valores
        raceOrderWithTime = myRaceOrderWithTime;
        raceOrder = myRaceOrder;
    }


    float ComputeCarArcLength(int id)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        Vector3 carPos = this._players[id].transform.position;

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this._circuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

        this._debuggingSpheres[id].transform.position = carProj;

        /*si la vuelta actual es menor que 0 entonces se mantien la ditancia a la meta en un valor negativo o en caso
         de que se vuelva para atras una vez pasado el primer checkpoint, se calcula la dostancioa a la meta en con un 
         valor negativo*/
        if (this._players[id].CurrentLap < 0 || _players[id].CurrentLap == 0 && _players[id].lastControlPoint == 0)
        {
            minArcL -= _circuitController.CircuitLength;
        }
        else
        {
            minArcL += _circuitController.CircuitLength *
                       (_players[id].CurrentLap);
        }


        Debug.Log("distancia recorrida de " + _players[id].Name + " [" + minArcL + "]");

        return minArcL;
    }
}
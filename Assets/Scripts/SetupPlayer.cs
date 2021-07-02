using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Random = System.Random;
using TMPro;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class SetupPlayer : NetworkBehaviour
{
    #region variables

    //variables para sincronizar el color, el nombre y el id
    [SyncVar] private int _id;
    
    //nombre 
    [SyncVar(hook = nameof(HandleDisplayNameUpdated))] [SerializeField]
    private string _name = null;

    //color
    [SyncVar(hook = nameof(HandleDisplayColorUpdated))] [SerializeField]
    private Color displayColor;

    //texto que se muestra encima del jugador y referencia al cuerpo del jugador
    [SerializeField] private TMP_Text displayNameText = null;
    [SerializeField] private GameObject body = null;

    private UIManager _uiManager;
    private MyNetworkManager _networkManager;
    private PlayerController _playerController;
    public PlayerInfo _playerInfo;
    private PolePositionManager _polePositionManager;

    #endregion


    #region Start & Stop Callbacks

    /// <summary>
    /// This is invoked for NetworkBehaviour objects when they become active on the server.
    /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
    /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        _id = NetworkServer.connections.Count - 1;
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        _playerInfo.ID = _id;
        _playerInfo.Name = _name;
        _playerInfo.color = displayColor;
        _polePositionManager.AddPlayer(_playerInfo);
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
    }

    #endregion

    private void Awake()
    {
        _playerInfo = GetComponent<PlayerInfo>();
        _playerController = GetComponent<PlayerController>();
        _networkManager = FindObjectOfType<MyNetworkManager>();
        _polePositionManager = FindObjectOfType<PolePositionManager>();
        _uiManager = FindObjectOfType<UIManager>();
    }

    // Start is called before the first frame update
    void Start()
    {
        /*solo el jugador local asigna los eventos con los handlers de dichos eventos*/
        if (isLocalPlayer)
        {
            _playerController.enabled = true;
            _playerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            _playerController.OnLapChangeEvent += OnLapChangeEventHandler;
            _playerController.OnTimeChangeEvent += OnTimeChangeEventHandler;
            _polePositionManager.OnPositionChangeEvent += OnPositionChangeEventHandler;
            _playerController.OnVictoryEvent += OnVictoryEventHandler;

            ConfigureCamera();
            
            _uiManager.m_SetupPlayer = this; //se asigna al ui manager una referencia al player
        }        
    }

    /*manejador para el cambio de velocidad*/
    void OnSpeedChangeEventHandler(float speed)
    {
        _uiManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    /*manejador para el cambio de tiempo*/
    void OnTimeChangeEventHandler(float time, float timeTot)
    {
        _uiManager.UpdateTime(time, timeTot);
    }
    
    /*manejador para mostrar la pantalla de victoria*/
    void OnVictoryEventHandler(float time)
    {
        CmdUpdateTime(time);
        _uiManager.UpdateVictory(); 
    }

    /*manejador para el cambio de vuelta*/
    void OnLapChangeEventHandler(int lap)
    {
        _uiManager.UpdateLap((int) _playerInfo.CurrentLap); // update laps
    }

    /*manejador para el cambio de posiciones*/
    void OnPositionChangeEventHandler(string posiciones,string posicionesTiempo)
    {
        _uiManager.UpdatePosiciones((string) posiciones,(string) posicionesTiempo); // update laps
    }
    
    /*manejador para el cambio de nombre del jugador*/
    private void HandleDisplayNameUpdated(string oldDisplayName, string newDisplayName)
    {
        displayNameText.text = newDisplayName;
    }
    
    /*manejador para el cambio de color*/

    private void HandleDisplayColorUpdated(Color oldDisplayColor, Color newDisplayColor)
    {
        body.GetComponent<MeshRenderer>().materials[1].color = newDisplayColor;
    }

    void ConfigureCamera()
    {
        if (Camera.main != null) Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
    }
    
    /*el servidor indica el numero maximo de jugadores*/
    [Server]
    public void SetNumberOfPlayers(int number)
    {
        _polePositionManager.UpdateMaxPlayers(number);
    }

    /*el cliente guarda en el servidor el nombre y el color del jugador*/
    [Command]
    public void CmdSetNameColor(string newDisplayName, Color newDisplayColor)
    {
        SetDisplayName(newDisplayName);
        setDisplayMaterial(newDisplayColor);
    }
    
    /*desconectar clientes y host*/
    public void Disconnect()
    {
        if (isLocalPlayer)
        {            
            if (!isServerOnly && isServer)
            {
                _networkManager.StopHost();
            }
            else
            {
                _networkManager.StopClient();
            }  
        }
    }

    /*el cliente actualiza el nombre en el servidor*/
    [Command]
    public void CmdUpdateName()
    {
        _polePositionManager.UpdatePlayer(_id, _name);
        _polePositionManager.numPlayers++;
    }
    
    /*el cliente actualiza el tiempo total en el servidor*/
    [Command]
    public void CmdUpdateTime(float time)
    {
        _polePositionManager.UpdateTime(_id, time);
    }

    /*el servidor indica el nombre del jugador*/
    [Server]
    public void SetDisplayName(string newDisplayName)
    {
        _name = newDisplayName;
    }
    
    /*el servidor indica el color del jugador*/
    [Server]
    public void setDisplayMaterial(Color newDisplayColor)
    {
        displayColor = newDisplayColor;
    }
    
    /*devuelve el nombre*/
    public string GetDisplayName()
    {
        return _name;
    }
}
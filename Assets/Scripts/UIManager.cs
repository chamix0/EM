using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public bool showGUI = true;

    private MyNetworkManager m_NetworkManager;
    private PolePositionManager m_PoleManager;
    public SetupPlayer m_SetupPlayer;
    public int numberPlayers;

    //main menu
    [Header("Main Menu")] [SerializeField] private GameObject mainMenu;
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonServer;
    [SerializeField] private InputField inputFieldIP;
    [SerializeField] private Button buttonTrain;

    //Number of players
    [Header("Number of Players")] [SerializeField]
    private GameObject numberOfPlayers;

    [SerializeField] private InputField inputFieldNumber;
    [SerializeField] private Button confirmPlayers;

    //Number of players server only
    [Header("Number of Players in Server")] [SerializeField]
    private GameObject numberOfPlayersServer;

    [SerializeField] private InputField inputFieldNumberServer;
    [SerializeField] private Button confirmPlayersServer;

    //color y Nombre
    [Header("Name and Color Selection")] [SerializeField]
    private GameObject nombreYColor;

    [SerializeField] private InputField inputFieldName;
    [SerializeField] private Button nextColor;
    [SerializeField] private Button previousColor;
    [SerializeField] private Button confirm;
    [SerializeField] private Button confirmSelection;
    [SerializeField] private Button renderColor;

    //pantalla victoria
    [Header("Victory")] [SerializeField] private GameObject pantallaFinal;
    [SerializeField] private Text textMarcador;
    [SerializeField] private Button exit;

    //HUD in game
    [Header("In-Game HUD")] [SerializeField]
    private GameObject inGameHUD;

    private string posVictoria;

    [SerializeField] private Text textSpeed;
    [SerializeField] private Text textLaps;
    [SerializeField] private Text textPosition;
    [SerializeField] private Text textTime;

    #region variables

    private Color[] colores = {Color.magenta, Color.green, Color.black, Color.red, Color.blue, Color.yellow};
    private int colorIndex;
    private int MAX_INDEX;
    private String playerName;

    #endregion

    private void Awake()
    {
        //inicializar variables
        numberPlayers = 1;
        colorIndex = 0;
        MAX_INDEX = colores.Length;
        renderColor.GetComponent<Image>().color = colores[colorIndex];
        playerName = "Anonymous";
        m_NetworkManager = FindObjectOfType<MyNetworkManager>();
        m_PoleManager = FindObjectOfType<PolePositionManager>();
    }

    private void Start()
    {
        //main menu
        buttonHost.onClick.AddListener(() => StartHost());
        buttonClient.onClick.AddListener(() => StartClient());
        buttonServer.onClick.AddListener(() => StartServer());
        buttonTrain.onClick.AddListener(() => StartTrain());

        //Number of players
        inputFieldNumber.onEndEdit.AddListener((number) => GetNumberPlayers(number));
        confirmPlayers.onClick.AddListener(() => SetNumberPlayers());

        //Number of players Server only
        inputFieldNumberServer.onEndEdit.AddListener((number) => GetNumberPlayers(number));
        confirmPlayersServer.onClick.AddListener(() => SetNumberPlayersServer());

        //color and name
        nextColor.onClick.AddListener(() => GetNextColor());
        previousColor.onClick.AddListener(() => GetPreviousColor());
        confirm.onClick.AddListener(() => ActivateInGameHUD());
        inputFieldName.onEndEdit.AddListener((nameP) => SetName(nameP));
        confirmSelection.onClick.AddListener(() => CreatePlayer());

        //vitoria
        exit.onClick.AddListener(() => ExitClient());

        ActivateMainMenu();
    }

    public void UpdateVictory()
    {
        textMarcador.text = "Fin de la partida\n" + posVictoria;
        ActivateVictoryMenu();
    }

    public void UpdateTime(float time, float lapTime)
    {
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time % 60);
        float milis = Mathf.FloorToInt((time * 100) % 100);

        string timeText = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, milis);
        float minutesLap = Mathf.FloorToInt(lapTime / 60);
        float secondsLap = Mathf.FloorToInt(lapTime % 60);
        float milisLap = Mathf.FloorToInt((lapTime * 100) % 100);

        string timeLap = string.Format("{0:00}:{1:00}.{2:00}", minutesLap, secondsLap, milisLap);

        textTime.text = "Tiempo total: " + timeText;
        textTime.text += "\n Tiempo vuelta: " + timeLap;
    }

    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    public void UpdateLap(int lap)
    {
        textLaps.text = "Lap " + (lap + 1) + "/3";
    }

    public void UpdatePosiciones(string posiciones, string posicionesTiempo)
    {
        posVictoria = posicionesTiempo;
        textPosition.text = posiciones;
    }

    private void ActivateVictoryMenu()
    {
        pantallaFinal.SetActive(true);
        mainMenu.SetActive(false);
        inGameHUD.SetActive(false);
        numberOfPlayers.SetActive(false);
        numberOfPlayersServer.SetActive(false);
        nombreYColor.SetActive(false);
    }

    #region activate UI

    private void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
        numberOfPlayers.SetActive(false);
        numberOfPlayersServer.SetActive(false);
        nombreYColor.SetActive(false);
    }

    private void ClientSelectName()
    {
        mainMenu.SetActive(false);
        numberOfPlayers.SetActive(false);
        nombreYColor.SetActive(true);
    }

    private void ActivateInGameHUD()
    {
        m_SetupPlayer.CmdUpdateName();
        numberOfPlayers.SetActive(false);
        nombreYColor.SetActive(false);
        mainMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }

    private void ServerOnlyActivateInGameHUD()
    {
        numberOfPlayers.SetActive(false);
        numberOfPlayersServer.SetActive(false);
        nombreYColor.SetActive(false);
        mainMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }
    
    #endregion


    private void NumberSelectionHUD()
    {
        nombreYColor.SetActive(false);
        mainMenu.SetActive(false);
        inGameHUD.SetActive(false);
        numberOfPlayers.SetActive(true);
    }

    private void ServerNumberSelectionHUD()
    {
        nombreYColor.SetActive(false);
        mainMenu.SetActive(false);
        inGameHUD.SetActive(false);
        numberOfPlayers.SetActive(false);
        numberOfPlayersServer.SetActive(true);
    }

    private void StartHost()
    {
        m_NetworkManager.StartHost();
        NumberSelectionHUD();
    }

    private void StartTrain()
    {
        //m_NetworkManager.ServerChangeScene(m_NetworkManager.offlineScene);
        SceneManager.LoadScene(1);
        NumberSelectionHUD();
    }

    private void StartClient()
    {
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = inputFieldIP.text;
        ClientSelectName();
    }

    private void StartServer()
    {
        m_NetworkManager.StartServer();
        ServerNumberSelectionHUD();
    }

    private void CreatePlayer()
    {
        m_SetupPlayer.CmdSetNameColor(playerName, colores[colorIndex]);
    }

    #region metodos locales

    private void GetNextColor()
    {
        colorIndex++;
        colorIndex %= MAX_INDEX;
        renderColor.GetComponent<Image>().color = colores[colorIndex];
    }

    private void GetPreviousColor()
    {
        colorIndex--;
        colorIndex = colorIndex < 0 ? MAX_INDEX - 1 : colorIndex;
        colorIndex %= MAX_INDEX;
        renderColor.GetComponent<Image>().color = colores[colorIndex];
    }

    private void ExitClient()
    {
        m_SetupPlayer.Disconnect();
        SceneManager.LoadScene(0);
    }
    
    private void SetName(string newName)
    {
        playerName = newName;
    }

    private void GetNumberPlayers(string number)
    {
        if (!int.TryParse(number, out numberPlayers))
        {
            numberPlayers = -1;
        }
    }

    private void SetNumberPlayers()
    {
        if (numberPlayers < 5 && numberPlayers > 1)
        {
            m_SetupPlayer.SetNumberOfPlayers(numberPlayers);
            ClientSelectName();
        }
    }

    private void SetNumberPlayersServer()
    {
        if (numberPlayers < 5 && numberPlayers > 1)
        {
            m_PoleManager.UpdateMaxPlayers(numberPlayers);
            ServerOnlyActivateInGameHUD();
        }
    }

    #endregion
}
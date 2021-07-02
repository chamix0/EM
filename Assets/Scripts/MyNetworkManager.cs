using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MyNetworkManager : NetworkManager
{
    public PolePositionManager _polePositionManager;

    #region Client

    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);

        Debug.Log(message: "[CLIENT] Jugador conectado");
    }

    #endregion

    #region Server

    /*cuando se conecta un jugador, se establece un nombre y un color aleatorio hasta que se asigne y se añade al polepositionmanager*/
    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        base.OnServerAddPlayer(conn);
        SetupPlayer player = conn.identity.GetComponent<SetupPlayer>();
        player.SetDisplayName("Player " + numPlayers);
        player.setDisplayMaterial(new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f)));
        _polePositionManager.AddPlayer(player._playerInfo);
        Debug.Log(message: "[SERVER] Se ha conectado: " + player.GetDisplayName());
    }
    
    /*cuando se desconecta el cliente del servidor le lleva al menu inicial*/
    private void OnDisconnectedFromServer(NetworkIdentity info)
    {
        SceneManager.LoadScene(0);
    }

    #endregion
}
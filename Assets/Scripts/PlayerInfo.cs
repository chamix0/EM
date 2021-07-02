using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerInfo : MonoBehaviour, IComparable<PlayerInfo>
{
    //numero de jugadores total en la carrera
    public int numJugadores { get; set; }

    //nombre del jugador
    public string Name { get; set; }

    //id del jugador
    public int ID { get; set; }

    //posicion actual del jugador (solo se actualiza cuando se termina la carrera)
    public int CurrentPosition { get; set; }

    //distancia recorrida
    public float CurrentProgress { get; set; }

    //color del jugador
    public Color color { get; set; }

    //tiempo total que lleva recorrido el jugador hasta que cruce la linea de meta
    public float timeTotal { get; set; }

    //ultimo punto de control superado por el jugador
    public int lastControlPoint { get; set; }

    //lista con los Bounds de la escena que corresponden con los puntos de control
    public List<Bounds> controlPoints { get; set; }

    //vuelta actual en la se encuentra el jugador
    public int CurrentLap { get; set; }

    //comparador ordenar el orden de lista, ya que es mas eficiente que la clase implementada en el pole position(la hemos borrado)
    public int CompareTo(PlayerInfo other)
    {
        if (other == null)
            return 1;
        else
            return this.CurrentProgress.CompareTo(other.CurrentProgress);
    }

    public override string ToString()
    {
        return Name;
    }
}
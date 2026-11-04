using System;
using UnityEngine;

/// <summary>
/// Un tramo de la pista, como lo recorrería una "tortuga": avanza unos metros, gira y baja.
/// Además dice qué hay en el tramo (vegetación, rocas, raíces, saltos, agua).
/// Se edita en el Inspector dentro de un TrackLayout ("Diseño de pista").
/// </summary>
[Serializable]
public class TrackSection
{
    // Los valores nuevos van siempre al final: así no cambian los tramos ya guardados.
    public enum Vegetation
    {
        Normal,
        Densa,
        Escasa,
        Ninguna,
        Verde,
        Canaveral
    }

    public enum Ground
    {
        Tierra,
        Carretera
    }

    public enum NaturalJump
    {
        Ninguno,
        PiedraIzquierda,
        PiedraDerecha,
        Raiz
    }

    public enum Ramp
    {
        Ninguna,
        Pequena,
        Grande,
        Concreto
    }

    [Tooltip("Nombre del tramo (solo para orientarse).")]
    [SerializeField] private string name = "Tramo";

    [Tooltip("Largo del tramo en metros.")]
    [SerializeField, Min(1f)] private float length = 30f;

    [Tooltip("Grados que gira en total a lo largo del tramo: positivo = derecha, negativo = izquierda. " +
             "Una horquilla es ±150 en unos 20-25 m.")]
    [SerializeField, Range(-200f, 200f)] private float turn;

    [Tooltip("Grados de bajada (0 = plano, 12 = muy empinado). Negativo = subida.")]
    [SerializeField, Range(-10f, 25f)] private float slope = 6f;

    [Tooltip("Medio ancho del camino en metros (1.9 = sendero angosto, 3.4 = ancho). 0 = el ancho por defecto del diseño.")]
    [SerializeField, Min(0f)] private float halfWidth;

    [Tooltip("Tierra = sendero o camino de tierra; Carretera = calle o carretera de lastre (más lisa y sin bermas).")]
    [SerializeField] private Ground ground = Ground.Tierra;

    [Header("Qué hay en el tramo")]
    [Tooltip("Casas y postes de luz a los lados (pueblo).")]
    [SerializeField] private bool buildings;

    [Tooltip("Árboles y matorral a los costados. Verde = vegetación tropical; Cañaveral = caña de azúcar alta.")]
    [SerializeField] private Vegetation vegetation = Vegetation.Normal;

    [Tooltip("Rocas en los bordes que angostan el camino (con collider).")]
    [SerializeField] private bool rocksOnEdges;

    [Tooltip("Pedregal: piedras sueltas dentro del camino (con collider) que hay que esquivar o pasar por encima.")]
    [SerializeField] private bool rockGarden;

    [Tooltip("Raíces cruzando el camino: tramo trabado que hace saltar un poco la bici.")]
    [SerializeField] private bool roots;

    [Tooltip("Salto natural opcional: una piedra en un borde que sirve de rampa, o una raíz grande de lado a lado.")]
    [SerializeField] private NaturalJump naturalJump = NaturalJump.Ninguno;

    [Tooltip("Dónde va el salto natural, como fracción del tramo (0 = inicio, 1 = final).")]
    [SerializeField, Range(0f, 1f)] private float jumpPosition = 0.6f;

    [Tooltip("Rampa en el centro del camino: de madera (pequeña o grande) o de concreto.")]
    [SerializeField] private Ramp ramp = Ramp.Ninguna;

    [Tooltip("Dónde va la rampa, como fracción del tramo.")]
    [SerializeField, Range(0f, 1f)] private float rampPosition = 0.6f;

    [Tooltip("Cruce de río: agua que frena la bici y piedras alrededor.")]
    [SerializeField] private bool water;

    public string Name => name;
    public float Length => length;
    public float Turn => turn;
    public float Slope => slope;
    public float HalfWidth => halfWidth;
    public Vegetation Plants => vegetation;
    public Ground Surface => ground;
    public bool Buildings => buildings;
    public bool RocksOnEdges => rocksOnEdges;
    public bool RockGarden => rockGarden;
    public bool Roots => roots;
    public NaturalJump Jump => naturalJump;
    public float JumpPosition => jumpPosition;
    public Ramp WoodRamp => ramp;
    public float RampPosition => rampPosition;
    public bool Water => water;

    public TrackSection()
    {
    }

    public TrackSection(string name, float length, float turn, float slope, float halfWidth = 0f)
    {
        this.name = name;
        this.length = length;
        this.turn = turn;
        this.slope = slope;
        this.halfWidth = halfWidth;
    }

    // Métodos encadenables para escribir diseños por código de forma legible.
    public TrackSection WithVegetation(Vegetation value) { vegetation = value; return this; }
    public TrackSection WithRocks() { rocksOnEdges = true; return this; }
    public TrackSection WithRoots() { roots = true; return this; }
    public TrackSection WithRockGarden() { rockGarden = true; return this; }
    public TrackSection WithJump(NaturalJump value, float position = 0.6f) { naturalJump = value; jumpPosition = position; return this; }
    public TrackSection WithRamp(Ramp value, float position = 0.6f) { ramp = value; rampPosition = position; return this; }
    public TrackSection WithWater() { water = true; return this; }
    public TrackSection AsRoad() { ground = Ground.Carretera; return this; }
    public TrackSection WithBuildings() { buildings = true; return this; }
}

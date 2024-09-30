using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class TerrainGenerator : MonoBehaviour
{
    // Mesh object for storing terrain data
    Mesh mesh;
    private int MESH_SCALE = 500; // Scale for visualizing mesh
    public AnimationCurve mandelbrotInfluenceCurve; // Mandelbrot influence over terrain
    public AnimationCurve heightCurve; // terrain height based on Perlin noise
    private Vector3[] vertices; // mesh vertices
    private int[] triangles; // triangles for the mesh

    // Size of the terrain grid
    public int xSize;
    public int zSize;

    // Parameters for noise-based terrain generation
    public float scale;
    public int octaves;
    public float lacunarity; // Controls frequency of Perlin noise layers
    public float waterHeight = 50f; // Height for water placement
    public float baseHeight = 0f; // Base terrain height

    // Seed for random number generation
    public int seed;
    private System.Random prng;
    private Vector2[] octaveOffsets; // random offsets for each octave in Perlin noise
    private float mandelbrotZoom; // Zoom for Mandelbrot fractal generation

    public GameObject waterPrefab;

    // Variables to handle mask reflection and rotation
    private float rotationAngle;
    private bool reflectX;
    private bool reflectZ;

    // Toggle for using Julia sets instead of Mandelbrot 
    public bool useJuliaSets = true;

    // Julia set parameters
    private Vector2 juliaConstant;
    private Vector2 juliaConstant2;
    private Vector2[] islandCenters; // Centers for island-like features
    public int islandCount = 2;

    void Start()
    {
        // Randomly set a rotation angle (multiples of 90 degrees for simplicity, can be changed)
        rotationAngle = Random.Range(0, 4) * 90f;
        Debug.Log("Mandelbrot rotation angle: " + rotationAngle);

        // Generate constants for Julia sets
        juliaConstant = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        juliaConstant2 = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));

        // Generate island centers
        islandCenters = new Vector2[islandCount];
        for (int i = 0; i < islandCount; i++)
        {
            islandCenters[i] = new Vector2(Random.Range(0, xSize * scale / 10), Random.Range(0, zSize * scale / 10));
        }

        // Terrain reflection probability decision along X and Z axes
        reflectX = Random.value > 0.5f;
        reflectZ = Random.value > 0.5f;

        // Set a zoom level for the Mandelbrot generation
        mandelbrotZoom = Random.Range(0.3f, 0.9f);

        // Create the mesh and water plane
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        CreateNewMap();
        CreateWaterPlane();
    }

    // Generates terrain mesh
    public void CreateNewMap()
    {
        CreateMeshShape();
        CreateTriangles();
        UpdateMesh();
    }

    // Creates the vertices of the mesh based on Perlin noise
    private void CreateMeshShape()
    {
        Vector2[] octaveOffsets = GetOffsetSeed();

        // Avoiding division by 0 since scale is public
        if (scale <= 0) scale = 0.0001f;

        // Array for vertices based on grid size
        vertices = new Vector3[(xSize + 1) * (zSize + 1)];

        // Loops through each grid point, generates a noise height, and assigns to the vertex
        for (int i = 0, z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                float noiseHeight = GenerateNoiseHeight(z, x, octaveOffsets);
                vertices[i] = new Vector3(x, noiseHeight, z); 
                i++;
            }
        }
    }

    // Returns an array of random offsets for each octave of noise
    private Vector2[] GetOffsetSeed()
    {
        seed = Random.Range(0, 1000); // Random seed for noise, can be set for repeated results

        prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        // Generates offsets for each octave
        for (int o = 0; o < octaves; o++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            octaveOffsets[o] = new Vector2(offsetX, offsetY);
        }
        return octaveOffsets;
    }

    // Generates a mask based on Mandelbrot set
    private float GenerateMandelbrotMask(float x, float z, int maxIterations)
    {
        // Reflects coordinates if required
        if (reflectX) x = xSize - x;
        if (reflectZ) z = zSize - z;

        // Rotates the coordinates
        Vector2 rotatedCoords = RotateCoordinates(x, z, rotationAngle);
        x = rotatedCoords.x;
        z = rotatedCoords.y;

        // Normalizes coordinates to fit Mandelbrot set
        float real = (x / xSize) * mandelbrotZoom * 1.5f - 0.5f;
        float imaginary = (z / zSize) * mandelbrotZoom * 1.5f - 0.25f;

        // Performs Mandelbrot iterations
        int iterations = 0;
        float a = 0, b = 0;
        while (iterations < maxIterations && (a * a + b * b) < 4.0f)
        {
            float tempA = a * a - b * b + real;
            b = 2.0f * a * b + imaginary;
            a = tempA;
            iterations++;
        }

        // Returns normalized iteration count as the mask
        return Mathf.Clamp01((float)iterations / maxIterations);
    }

    // Generates a mask based on Julia set, almost the same as Mandelbrot 
    private float GenerateJuliaMask(float x, float z, int maxIterations, Vector2 constant)
    {
        float real = (x / xSize) * 3f - 1.5f;
        float imaginary = (z / zSize) * 3f - 1f;

        float a = real, b = imaginary;
        int iterations = 0;
        float cReal = constant.x, cImaginary = constant.y;

        while (iterations < maxIterations && (a * a + b * b) < 4.0f)
        {
            float tempA = a * a - b * b + cReal;
            b = 2.0f * a * b + cImaginary;
            a = tempA;
            iterations++;
        }

        return Mathf.Clamp01((float)iterations / maxIterations);
    }

    // Combines Perlin noise and fractal masks
    private float GenerateNoiseHeight(int z, int x, Vector2[] octaveOffsets)
    {
        float amplitude = 12, frequency = 1, persistence = 0.5f;
        float noiseHeight = 0;

        // Combines multiple layers of Perlin noise
        for (int y = 0; y < octaves; y++)
        {
            float mapZ = z / scale * frequency + octaveOffsets[y].y;
            float mapX = x / scale * frequency + octaveOffsets[y].x;
            float perlinValue = (Mathf.PerlinNoise(mapZ, mapX)) * 2 - 1;
            noiseHeight += heightCurve.Evaluate(perlinValue) * amplitude;
            frequency *= lacunarity;
            amplitude *= persistence;
        }

        // Applies either Julia sets or Mandelbrot sets as a mask
        if (useJuliaSets)
        {
            float juliaMask1 = GenerateJuliaMask(x, z, 2000, juliaConstant);
            float juliaMask2 = GenerateJuliaMask(x, z, 2000, juliaConstant2);
            noiseHeight = Mathf.Lerp(baseHeight, noiseHeight, juliaMask1 + juliaMask2);
        }
        else
        {
            float mandelbrotMask = GenerateMandelbrotMask(x, z, 2000);
            // Intensity Perlin Noise for Mandelbrot
            float perlinMaskFrequency = 0.05f;
            float perlinMask = Mathf.PerlinNoise(x * perlinMaskFrequency, z * perlinMaskFrequency);
            float blendedMask = Mathf.Lerp(0f, mandelbrotMask, perlinMask);
            float blendFactor = mandelbrotInfluenceCurve.Evaluate(blendedMask);
            // Final mask
            noiseHeight = Mathf.Lerp(baseHeight, noiseHeight, blendFactor);
        }

        return noiseHeight;
    }

    // Generates triangles for the mesh from the vertices
    private void CreateTriangles()
    {
        triangles = new int[xSize * zSize * 6];
        int vert = 0, tris = 0;

        for (int z = 0; z < xSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                triangles[tris + 0] = vert + 0;
                triangles[tris + 1] = vert + xSize + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + xSize + 1;
                triangles[tris + 5] = vert + xSize + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }
    }

    // Updates the mesh with the newly generated vertices and triangles
    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        GetComponent<MeshCollider>().sharedMesh = mesh;

        // Scales the terrain for better visualization
        gameObject.transform.localScale = new Vector3(MESH_SCALE, MESH_SCALE, MESH_SCALE);
    }

    // Creates and positions a water plane at the waterHeight
    private void CreateWaterPlane()
    {
        // Should instatiate in the center of terrain but can be moved for interactivity 
        GameObject waterPlane = Instantiate(waterPrefab, new Vector3(scale * scale * 10, waterHeight, scale * scale * 10), Quaternion.identity);
        waterPlane.transform.localScale = new Vector3(scale * xSize, 1, scale * zSize);
    }

    // Rotates coordinates by a given angle
    private Vector2 RotateCoordinates(float x, float z, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float rotatedX = x * Mathf.Cos(radians) - z * Mathf.Sin(radians);
        float rotatedZ = x * Mathf.Sin(radians) + z * Mathf.Cos(radians);

        return new Vector2(rotatedX, rotatedZ);
    }
}

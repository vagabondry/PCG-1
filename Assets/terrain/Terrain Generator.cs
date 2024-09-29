using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class TerrainGenerator : MonoBehaviour
{
    Mesh mesh;
    private int MESH_SCALE = 500;
    public AnimationCurve mandelbrotInfluenceCurve;
    public AnimationCurve heightCurve;
    private Vector3[] vertices;
    private int[] triangles;

    public int xSize;
    public int zSize;

    public float scale;
    public int octaves;
    public float lacunarity;
    public float waterHeight = 50f; 
    public float baseHeight = 0f;

    public int seed;
    private System.Random prng;
    private Vector2[] octaveOffsets;
    private float mandelbrotZoom;

    public GameObject waterPrefab;

    private float rotationAngle; 
    private bool reflectX;
    private bool reflectZ;

    public bool useJuliaSets = true; 

    // Julia sets parameters
    private Vector2 juliaConstant;
    private Vector2 juliaConstant2;
    private Vector2[] islandCenters; 
    public int islandCount = 2;

    void Start()
    {
        rotationAngle = Random.Range(0, 4) * 90f;
        Debug.Log("Mandelbrot rotation angle: " + rotationAngle);

        juliaConstant = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        Debug.Log("Julia constant: " + juliaConstant);
        juliaConstant2 = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        Debug.Log("Julia constant: " + juliaConstant2);

        islandCenters = new Vector2[islandCount];
        for (int i = 0; i < islandCount; i++)
        {
            islandCenters[i] = new Vector2(Random.Range(0, xSize * scale / 10), Random.Range(0, zSize * scale / 10));
            Debug.Log("Island " + (i + 1) + " center: " + islandCenters[i]);
        }

        reflectX = Random.value > 0.5f;
        reflectZ = Random.value > 0.5f;

        mandelbrotZoom = Random.Range(0.3f, 0.9f);
        Debug.Log("Mandelbrot zoom: " + mandelbrotZoom);

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        CreateNewMap();
        CreateWaterPlane();
    }

    public void CreateNewMap()
    {
        CreateMeshShape();
        CreateTriangles();
        UpdateMesh();
    }

    private void CreateMeshShape()
    {
        Vector2[] octaveOffsets = GetOffsetSeed();

        if (scale <= 0)
            scale = 0.0001f;

        vertices = new Vector3[(xSize + 1) * (zSize + 1)];

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

    private Vector2[] GetOffsetSeed()
    {
        seed = Random.Range(0, 1000);
        Debug.Log("seed: " + seed);

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        for (int o = 0; o < octaves; o++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            octaveOffsets[o] = new Vector2(offsetX, offsetY);
        }
        return octaveOffsets;
    }

    private float GenerateMandelbrotMask(float x, float z, int maxIterations)
    {
        if (reflectX) x = xSize - x;
        if (reflectZ) z = zSize - z;

        Vector2 rotatedCoords = RotateCoordinates(x, z, rotationAngle);
        x = rotatedCoords.x;
        z = rotatedCoords.y;

        float real = (x / xSize) * mandelbrotZoom * 1.5f - 0.5f;
        float imaginary = (z / zSize) * mandelbrotZoom * 1.5f - 0.25f;

        int iterations = 0;
        float a = 0;
        float b = 0;

        while (iterations < maxIterations && (a * a + b * b) < 4.0f)
        {
            float tempA = a * a - b * b + real;
            b = 2.0f * a * b + imaginary;
            a = tempA;
            iterations++;
        }

        return Mathf.Clamp01((float)iterations / maxIterations);
    }

    private float GenerateJuliaMask(float x, float z, int maxIterations, Vector2 constant)
    {
        float real = (x / xSize) * 3f - 1.5f;
        float imaginary = (z / zSize) * 3f - 1f;

        float a = real;
        float b = imaginary;

        int iterations = 0;
        float cReal = constant.x;
        float cImaginary = constant.y;

        while (iterations < maxIterations && (a * a + b * b) < 4.0f)
        {
            float tempA = a * a - b * b + cReal;
            b = 2.0f * a * b + cImaginary;
            a = tempA;
            iterations++;
        }

        return Mathf.Clamp01((float)iterations / maxIterations);
    }

    private float GenerateNoiseHeight(int z, int x, Vector2[] octaveOffsets)
    {
        float amplitude = 12;
        float frequency = 1;
        float persistence = 0.5f;
        float noiseHeight = 0;

        for (int y = 0; y < octaves; y++)
        {
            float mapZ = z / scale * frequency + octaveOffsets[y].y;
            float mapX = x / scale * frequency + octaveOffsets[y].x;

            float perlinValue = (Mathf.PerlinNoise(mapZ, mapX)) * 2 - 1;
            noiseHeight += heightCurve.Evaluate(perlinValue) * amplitude;
            frequency *= lacunarity;
            amplitude *= persistence;
        }

        if (useJuliaSets)
        {
            float juliaMask1 = GenerateJuliaMask(x, z, 2000, juliaConstant);
            float juliaMask2 = GenerateJuliaMask(x, z, 2000, juliaConstant2);

            noiseHeight = Mathf.Lerp(baseHeight, noiseHeight, juliaMask1 + juliaMask2);
        }
        else
        {
            float mandelbrotMask = GenerateMandelbrotMask(x, z, 2000);

            float perlinMaskFrequency = 0.05f;
            float perlinMask = Mathf.PerlinNoise(x * perlinMaskFrequency, z * perlinMaskFrequency);

            float blendedMask = Mathf.Lerp(0f, mandelbrotMask, perlinMask);
            float blendFactor = mandelbrotInfluenceCurve.Evaluate(blendedMask);
            noiseHeight = Mathf.Lerp(baseHeight, noiseHeight, blendFactor);
        }

        return noiseHeight;
    }

    private void CreateTriangles()
    {
        triangles = new int[xSize * zSize * 6];
        int vert = 0;
        int tris = 0;

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

    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        GetComponent<MeshCollider>().sharedMesh = mesh;

        gameObject.transform.localScale = new Vector3(MESH_SCALE, MESH_SCALE, MESH_SCALE);
    }

    private void CreateWaterPlane()
    {
        GameObject waterPlane = Instantiate(waterPrefab, new Vector3(scale * scale * 10, waterHeight, scale * scale * 10), Quaternion.identity);
        waterPlane.transform.localScale = new Vector3(scale * xSize, 1, scale * zSize);
    }

    private Vector2 RotateCoordinates(float x, float z, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float rotatedX = x * Mathf.Cos(radians) - z * Mathf.Sin(radians);
        float rotatedZ = x * Mathf.Sin(radians) + z * Mathf.Cos(radians);

        return new Vector2(rotatedX, rotatedZ);
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Chunk
{
    private readonly ChunkSystem chunkSystem;

    private bool chunkGameObjectLoaded;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    public const int ChunkSize = 16;
    public const int ChunkHeight = 256;

    private readonly DataTypes.Block[,,] chunkData = new DataTypes.Block[ChunkSize, ChunkHeight, ChunkSize];
    private readonly Vector3Int chunkPosition;

    public Chunk(ChunkSystem chunkSystem, Vector3Int chunkPosition)
    {
        this.chunkSystem = chunkSystem;

        this.chunkPosition = chunkPosition;
        this.chunkPosition.y = 0;

        for (var y = 0; y < ChunkHeight; y++)
        {
            if (y >= 128 + chunkPosition.z + chunkPosition.x) continue; // keep air
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var z = 0; z < ChunkSize; z++)
                {
                    chunkData[x, y, z] = DataTypes.Block.Grass;
                }
            }
        }
    }

    public void SetBlock(Vector3Int positionInChunk, DataTypes.Block block)
    {
        chunkData[positionInChunk.x, positionInChunk.y, positionInChunk.z] = block;
    }

    public DataTypes.Block GetBlock(Vector3Int positionInChunk)
    {
        if (positionInChunk.x is < 0 or > ChunkSize - 1 || positionInChunk.y is < 0 or > ChunkHeight - 1 ||
            positionInChunk.z is < 0 or > ChunkSize - 1) return DataTypes.Block.Air;
        return chunkData[positionInChunk.x, positionInChunk.y, positionInChunk.z];
    }

    // 0 --- 1
    // |     |
    // 3 --- 2

    private static readonly Dictionary<DataTypes.BlockSide, Vector3[]> SideVerts = new()
    {
        {DataTypes.BlockSide.Front, new Vector3[] {new(-1, 1, -1), new(1, 1, -1), new(1, -1, -1), new(-1, -1, -1)}},
        {DataTypes.BlockSide.Back, new Vector3[] {new(1, 1, 1), new(-1, 1, 1), new(-1, -1, 1), new(1, -1, 1)}},
        {DataTypes.BlockSide.Left, new Vector3[] {new(-1, 1, 1), new(-1, 1, -1), new(-1, -1, -1), new(-1, -1, 1)}},
        {DataTypes.BlockSide.Right, new Vector3[] {new(1, 1, -1), new(1, 1, 1), new(1, -1, 1), new(1, -1, -1)}},
        {DataTypes.BlockSide.Top, new Vector3[] {new(-1, 1, 1), new(1, 1, 1), new(1, 1, -1), new(-1, 1, -1)}},
        {DataTypes.BlockSide.Bottom, new Vector3[] {new(-1, -1, -1), new(1, -1, -1), new(1, -1, 1), new(-1, -1, 1)}},
    };

    private static readonly Dictionary<DataTypes.BlockSide, Vector3> SideNormals = new()
    {
        {DataTypes.BlockSide.Front, new Vector3(0, 0, -1)},
        {DataTypes.BlockSide.Back, new Vector3(0, 0, 1)},
        {DataTypes.BlockSide.Left, new Vector3(-1, 0, 0)},
        {DataTypes.BlockSide.Right, new Vector3(1, 0, 0)},
        {DataTypes.BlockSide.Top, new Vector3(0, 1, 0)},
        {DataTypes.BlockSide.Bottom, new Vector3(0, -1, 0)},
    };

    private class MeshAsLists
    {
        public List<Vector3> vertices;
        public List<Vector3> normals;
        public List<int> triangles;
        public List<Vector2> uv;
        public List<Color> colors;
    }

    private void AddSquareToCubeMesh(ref MeshAsLists mesh, Vector3Int blockPosition, DataTypes.Block block,
        DataTypes.BlockSide blockSide)
    {
        if (block == DataTypes.Block.Air) return;

        var sideVertices = SideVerts[blockSide];
        foreach (var index in new[] {0, 1, 2, 0, 2, 3})
        {
            mesh.vertices.Add(sideVertices[index] * 0.5f + blockPosition);
            mesh.triangles.Add(mesh.vertices.Count - 1);
            mesh.normals.Add(SideNormals[blockSide]);
        }
        
        var blockSideUv = DependencyManager.Instance.ChunkMaterialManager.GetBlockSideUv(block, blockSide);
        mesh.uv.Add(new Vector2(blockSideUv.xMin, blockSideUv.yMax));
        mesh.uv.Add(new Vector2(blockSideUv.xMax, blockSideUv.yMax));
        mesh.uv.Add(new Vector2(blockSideUv.xMax, blockSideUv.yMin));
        mesh.uv.Add(new Vector2(blockSideUv.xMin, blockSideUv.yMax));
        mesh.uv.Add(new Vector2(blockSideUv.xMax, blockSideUv.yMin));
        mesh.uv.Add(new Vector2(blockSideUv.xMin, blockSideUv.yMin));

        // grass has vertex color 0,1,0 so that the shader can do grass things
        if (block == DataTypes.Block.Grass && blockSide != DataTypes.BlockSide.Bottom)
        {
            mesh.colors.AddRange(new[] {Color.green, Color.green, Color.green, Color.green, Color.green, Color.green});
        }
        else
        {
            mesh.colors.AddRange(new[] {Color.black, Color.black, Color.black, Color.black, Color.black, Color.black});
        }
    }

    private bool IsAirAroundBlock(Vector3Int blockPositionInChunk, DataTypes.BlockSide blockSide)
    {
        var queryOffset = blockSide switch
        {
            DataTypes.BlockSide.Front => new Vector3Int(0, 0, -1),
            DataTypes.BlockSide.Back => new Vector3Int(0, 0, 1),
            DataTypes.BlockSide.Left => new Vector3Int(-1, 0, 0),
            DataTypes.BlockSide.Right => new Vector3Int(1, 0, 0),
            DataTypes.BlockSide.Top => new Vector3Int(0, 1, 0),
            DataTypes.BlockSide.Bottom => new Vector3Int(0, -1, 0),
            _ => Vector3Int.zero
        };

        var queryPositionInChunk = queryOffset + blockPositionInChunk;
        if (queryPositionInChunk.y is < 0 or > ChunkHeight - 1) return true; // top and bottom

        const int edge = ChunkSize - 1;

        switch (queryPositionInChunk.x)
        {
            case < 0:
                return chunkSystem.GetChunk(chunkPosition + queryOffset)
                    .GetBlock(new Vector3Int(edge, blockPositionInChunk.y, blockPositionInChunk.z)) == DataTypes.Block.Air;
            case > edge:
                return chunkSystem.GetChunk(chunkPosition + queryOffset)
                    .GetBlock(new Vector3Int(0, blockPositionInChunk.y, blockPositionInChunk.z)) == DataTypes.Block.Air;
        }
        
        switch (queryPositionInChunk.z)
        {
            case < 0:
                return chunkSystem.GetChunk(chunkPosition + queryOffset)
                    .GetBlock(new Vector3Int(blockPositionInChunk.x, blockPositionInChunk.y, edge)) == DataTypes.Block.Air;
            case > edge:
                return chunkSystem.GetChunk(chunkPosition + queryOffset)
                    .GetBlock(new Vector3Int(blockPositionInChunk.x, blockPositionInChunk.y, 0)) == DataTypes.Block.Air;
        }
        
        var queryBlock = chunkData[queryPositionInChunk.x, queryPositionInChunk.y, queryPositionInChunk.z];
        return queryBlock == DataTypes.Block.Air;
    }

    public void MakeChunkGameObject()
    {
        var chunkGameObject = new GameObject($"Chunk{chunkPosition.x},{chunkPosition.z}")
        {
            transform =
            {
                position = chunkPosition * ChunkSize
            },
            layer = LayerMask.NameToLayer("Chunk")
        };

        var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
       
        var atlasMaterial = DependencyManager.Instance.ChunkMaterialManager.GetAtlasMaterial();
        meshRenderer.material = atlasMaterial;

        meshFilter = chunkGameObject.AddComponent<MeshFilter>();
        // meshFilter.mesh = mesh;

        var rigidbody = chunkGameObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        meshCollider = chunkGameObject.AddComponent<MeshCollider>();
        // meshCollider.sharedMesh = mesh;

        chunkGameObjectLoaded = true;
    }

    public void GenerateMesh()
    {
        if (!chunkGameObjectLoaded) return;

        var mesh = new Mesh();

        var meshAsLists = new MeshAsLists()
        {
            vertices = mesh.vertices.ToList(),
            normals = mesh.normals.ToList(),
            triangles = mesh.triangles.ToList(),
            uv = mesh.uv.ToList(),
            colors = mesh.colors.ToList(),
        };

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkHeight; y++)
            {
                for (var z = 0; z < ChunkSize; z++)
                {
                    var position = new Vector3Int(x, y, z);
                    var block = chunkData[x, y, z];

                    if (IsAirAroundBlock(position, DataTypes.BlockSide.Front))
                        AddSquareToCubeMesh(ref meshAsLists, position, block, DataTypes.BlockSide.Front);
                    if (IsAirAroundBlock(position, DataTypes.BlockSide.Back))
                        AddSquareToCubeMesh(ref meshAsLists, position, block, DataTypes.BlockSide.Back);
                    if (IsAirAroundBlock(position, DataTypes.BlockSide.Left))
                        AddSquareToCubeMesh(ref meshAsLists, position, block, DataTypes.BlockSide.Left);
                    if (IsAirAroundBlock(position, DataTypes.BlockSide.Right))
                        AddSquareToCubeMesh(ref meshAsLists, position, block, DataTypes.BlockSide.Right);
                    if (IsAirAroundBlock(position, DataTypes.BlockSide.Top))
                        AddSquareToCubeMesh(ref meshAsLists, position, block, DataTypes.BlockSide.Top);
                    if (IsAirAroundBlock(position, DataTypes.BlockSide.Bottom))
                        AddSquareToCubeMesh(ref meshAsLists, position, block, DataTypes.BlockSide.Bottom);
                }
            }
        }

        mesh.vertices = meshAsLists.vertices.ToArray();
        mesh.normals = meshAsLists.normals.ToArray();
        mesh.triangles = meshAsLists.triangles.ToArray();
        mesh.uv = meshAsLists.uv.ToArray();
        mesh.colors = meshAsLists.colors.ToArray();
        mesh.Optimize();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
    }
}
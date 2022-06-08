using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ChunkBounds {
    public Vector3 topLeft;
    public Vector3 bottomLeft;
    public Vector3 topRight;
    public Vector3 bottomRight;

    public ChunkBounds(Vector3 topLeft, Vector3 bottomLeft, Vector3 topRight, Vector3 bottomRight)
    {
        this.topLeft = topLeft;
        this.bottomLeft = bottomLeft;
        this.topRight = topRight;
        this.bottomRight = bottomRight;
    }
}

public enum Side
{
    TOP, RIGHT, BOTTOM, LEFT
}

public struct PlaneMeshBorders {
    public int[] top, right, bottom, left;

    public PlaneMeshBorders(int dimension)
    {
        top = new int[dimension];
        right = new int[dimension];
        bottom = new int[dimension];
        left = new int[dimension];
    }

    public int[] GetSide(Side s)
    {
        switch (s) {
            case Side.TOP:
                return top;
            case Side.RIGHT:
                return right;
            case Side.BOTTOM:
                return bottom;
            case Side.LEFT:
                return left;
            default:
                return null;
        }
    }
}

[System.Serializable]
public struct ChunkBorder {
    public int otherChunk;
    public Side otherSide;

    public ChunkBorder(int otherChunk, Side otherSide)
    {
        this.otherChunk = otherChunk;
        this.otherSide = otherSide;
    }
}

[System.Serializable]
public class Chunk
{
    public ChunkBounds bounds;

    public Vector3 center;

    public Mesh mesh;

    public int currentLOD;

    public ChunkBorder[] borders;

    public bool meshUpdatePending;

    public static Mesh[] planeMeshes;

    public static PlaneMeshBorders[] planeMeshBorders;

    public Chunk(Vector3 topLeft, Vector3 bottomLeft, Vector3 topRight, Vector3 bottomRight)
    {
        bounds = new ChunkBounds(topLeft, bottomLeft, topRight, bottomRight);
        center = (topLeft + bottomLeft + topRight + bottomRight) / 4;
        mesh = new Mesh();
        currentLOD = -1;
        borders = new ChunkBorder[4];
        meshUpdatePending = true;
    }

    public static void GeneratePlaneMeshes(int count)
    {
        planeMeshes = new Mesh[count];
        planeMeshBorders = new PlaneMeshBorders[count];

        int dimension = 2;
        int vertexDimension = 3;

        for(int i = 0; i < count; i++, dimension *= 2, vertexDimension = dimension + 1)
        {
            Vector3[] vertices = new Vector3[vertexDimension * vertexDimension];
            int[] triangles = new int[dimension * dimension * 3 * 2];
            PlaneMeshBorders pmb = new PlaneMeshBorders(vertexDimension);

            float stepSize = 1.0f / dimension;
            Vector3 tempVertical = new Vector3(0, 0, 0);
            Vector3 horizontalStep = new Vector3(stepSize, 0, 0);
            Vector3 verticalStep = new Vector3(0, stepSize, 0);
            int vertexIndex = 0;
            int triangleIndex = 0;

            for (int j = 0; j < vertexDimension; j++)
            {
                Vector3 temp = tempVertical;
                for(int k = 0; k < vertexDimension; k++)
                {
                    vertices[vertexIndex] = temp;

                    if (j != dimension && k != dimension)
                    {
                        triangles[triangleIndex] = vertexIndex;
                        triangles[triangleIndex + 1] = vertexIndex + vertexDimension + 1;
                        triangles[triangleIndex + 2] = vertexIndex + vertexDimension;

                        triangles[triangleIndex + 3] = vertexIndex;
                        triangles[triangleIndex + 4] = vertexIndex + 1;
                        triangles[triangleIndex + 5] = vertexIndex + vertexDimension + 1;

                        triangleIndex += 6;
                    }

                    if(j == 0)
                    {
                        pmb.top[k] = vertexIndex;
                    }
                    if(j == dimension)
                    {
                        pmb.bottom[k] = vertexIndex;
                    }
                    if(k == 0)
                    {
                        pmb.left[j % vertexDimension] = vertexIndex;
                    }
                    if(k == dimension)
                    {
                        pmb.right[j % vertexDimension] = vertexIndex;
                    }

                    temp += horizontalStep;
                    vertexIndex++;
                }
                tempVertical += verticalStep;
            }

            Array.Reverse(pmb.bottom);
            Array.Reverse(pmb.left);

            planeMeshBorders[i] = pmb;

            planeMeshes[i] = new Mesh();
            planeMeshes[i].vertices = vertices;
            planeMeshes[i].triangles = triangles;
        }
        Array.Reverse(planeMeshes);
        Array.Reverse(planeMeshBorders);
    }

    public static Chunk[] GenerateChunks(int lod)
    {
        float s = 1 / Mathf.Sqrt(3);
        List<Chunk> chunks = new List<Chunk> {
            new Chunk(new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(-s, s, s), new Vector3(s, s, s)), // top 0
            new Chunk(new Vector3(s, s, -s), new Vector3(s, -s, -s), new Vector3(s, s, s), new Vector3(s, -s, s)), // front 1
            new Chunk(new Vector3(s, s, s), new Vector3(s, -s, s), new Vector3(-s, s, s), new Vector3(-s, -s, s)), // right 2
            new Chunk(new Vector3(-s, s, s), new Vector3(-s, -s, s), new Vector3(-s, s, -s), new Vector3(-s, -s, -s)), // back 3
            new Chunk(new Vector3(-s, s, -s), new Vector3(-s, -s, -s), new Vector3(s, s, -s), new Vector3(s, -s, -s)), // left 4
            new Chunk(new Vector3(s, -s, -s), new Vector3(-s, -s, -s), new Vector3(s, -s, s), new Vector3(-s, -s, s)) // bottom 5
        };

        int TOP = 0, FRONT = 1, RIGHT = 2, BACK = 3, LEFT = 4, BOTTOM = 5;

        chunks[TOP].borders[(int)Side.TOP] = new ChunkBorder(BACK, Side.TOP); // top side of top chunk is adjacent to top side of back chunk
        chunks[TOP].borders[(int)Side.RIGHT] = new ChunkBorder(RIGHT, Side.TOP);
        chunks[TOP].borders[(int)Side.BOTTOM] = new ChunkBorder(FRONT, Side.TOP);
        chunks[TOP].borders[(int)Side.LEFT] = new ChunkBorder(LEFT, Side.TOP);

        chunks[FRONT].borders[(int)Side.TOP] = new ChunkBorder(TOP, Side.BOTTOM);
        chunks[FRONT].borders[(int)Side.RIGHT] = new ChunkBorder(RIGHT, Side.LEFT);
        chunks[FRONT].borders[(int)Side.BOTTOM] = new ChunkBorder(BOTTOM, Side.TOP);
        chunks[FRONT].borders[(int)Side.LEFT] = new ChunkBorder(LEFT, Side.RIGHT);

        chunks[RIGHT].borders[(int)Side.TOP] = new ChunkBorder(TOP, Side.RIGHT);
        chunks[RIGHT].borders[(int)Side.RIGHT] = new ChunkBorder(BACK, Side.LEFT);
        chunks[RIGHT].borders[(int)Side.BOTTOM] = new ChunkBorder(BOTTOM, Side.RIGHT);
        chunks[RIGHT].borders[(int)Side.LEFT] = new ChunkBorder(FRONT, Side.RIGHT);

        chunks[BACK].borders[(int)Side.TOP] = new ChunkBorder(TOP, Side.TOP);
        chunks[BACK].borders[(int)Side.RIGHT] = new ChunkBorder(LEFT, Side.LEFT);
        chunks[BACK].borders[(int)Side.BOTTOM] = new ChunkBorder(BOTTOM, Side.BOTTOM);
        chunks[BACK].borders[(int)Side.LEFT] = new ChunkBorder(RIGHT, Side.RIGHT);

        chunks[LEFT].borders[(int)Side.TOP] = new ChunkBorder(TOP, Side.LEFT);
        chunks[LEFT].borders[(int)Side.RIGHT] = new ChunkBorder(FRONT, Side.LEFT);
        chunks[LEFT].borders[(int)Side.BOTTOM] = new ChunkBorder(BOTTOM, Side.LEFT);
        chunks[LEFT].borders[(int)Side.LEFT] = new ChunkBorder(BACK, Side.RIGHT);

        chunks[BOTTOM].borders[(int)Side.TOP] = new ChunkBorder(FRONT, Side.BOTTOM);
        chunks[BOTTOM].borders[(int)Side.RIGHT] = new ChunkBorder(RIGHT, Side.BOTTOM);
        chunks[BOTTOM].borders[(int)Side.BOTTOM] = new ChunkBorder(BACK, Side.BOTTOM);
        chunks[BOTTOM].borders[(int)Side.LEFT] = new ChunkBorder(LEFT, Side.BOTTOM);

        for (int i = 0; i < lod; i++)
        {
            List<Chunk> newChunks = new List<Chunk>();
            for (int j = 0; j < chunks.Count; j++)
            {
                Chunk parent = chunks[j];

                Vector3 top = Vector3.Normalize(parent.bounds.topLeft + parent.bounds.topRight);
                Vector3 right = Vector3.Normalize(parent.bounds.topRight + parent.bounds.bottomRight);
                Vector3 bottom = Vector3.Normalize(parent.bounds.bottomRight + parent.bounds.bottomLeft);
                Vector3 left = Vector3.Normalize(parent.bounds.bottomLeft + parent.bounds.topLeft);
                Vector3 center = Vector3.Normalize(parent.bounds.topLeft + parent.bounds.topRight + parent.bounds.bottomRight + parent.bounds.bottomLeft);

                Chunk topLeft = new Chunk(parent.bounds.topLeft, left, top, center);
                Chunk topRight = new Chunk(top, center, parent.bounds.topRight, right);
                Chunk bottomLeft = new Chunk(left, parent.bounds.bottomLeft, center, bottom);
                Chunk bottomRight = new Chunk(center, bottom, right, parent.bounds.bottomRight);

                topLeft.borders[(int)Side.RIGHT]= new ChunkBorder(newChunks.Count + 1, Side.LEFT);
                topLeft.borders[(int)Side.BOTTOM] = new ChunkBorder(newChunks.Count + 2, Side.TOP);
                topRight.borders[(int)Side.LEFT] = new ChunkBorder(newChunks.Count, Side.RIGHT);
                topRight.borders[(int)Side.BOTTOM] = new ChunkBorder(newChunks.Count + 3, Side.TOP);
                bottomLeft.borders[(int)Side.RIGHT] = new ChunkBorder(newChunks.Count + 3, Side.LEFT);
                bottomLeft.borders[(int)Side.TOP] = new ChunkBorder(newChunks.Count, Side.BOTTOM);
                bottomRight.borders[(int)Side.LEFT] = new ChunkBorder(newChunks.Count + 2, Side.RIGHT);
                bottomRight.borders[(int)Side.TOP] = new ChunkBorder(newChunks.Count + 1, Side.BOTTOM);

                newChunks.Add(topLeft);
                newChunks.Add(topRight);
                newChunks.Add(bottomLeft);
                newChunks.Add(bottomRight);
            }

            for (int j = 0; j < chunks.Count; j++) // for every chunk
            {
                int childrenIndex = j * 4;

                for(int k = 0; k < 4; k++) // for each border
                {
                    int[] borderChildIndices = new int[2]; // 2 children on this side of border
                    int[] adjacentChildIndices = new int[2]; // 2 children on other side of border

                    switch(k)
                    {
                        case (int)Side.TOP:
                            borderChildIndices[0] = childrenIndex;
                            borderChildIndices[1] = childrenIndex + 1;
                            break;
                        case (int)Side.RIGHT:
                            borderChildIndices[0] = childrenIndex + 1;
                            borderChildIndices[1] = childrenIndex + 3;
                            break;
                        case (int)Side.BOTTOM:
                            borderChildIndices[0] = childrenIndex + 3;
                            borderChildIndices[1] = childrenIndex + 2;
                            break;
                        case (int)Side.LEFT:
                            borderChildIndices[0] = childrenIndex + 2;
                            borderChildIndices[1] = childrenIndex;
                            break;
                    }

                    ChunkBorder border = chunks[j].borders[k];
                    int otherChunkChildrenIndex = border.otherChunk * 4;

                    switch(border.otherSide)
                    {
                        case Side.TOP:
                            adjacentChildIndices[0] = otherChunkChildrenIndex + 1; // top right
                            adjacentChildIndices[1] = otherChunkChildrenIndex; // top left
                            break;
                        case Side.RIGHT:
                            adjacentChildIndices[0] = otherChunkChildrenIndex + 3; // top right
                            adjacentChildIndices[1] = otherChunkChildrenIndex + 1; // bottom right
                            break;
                        case Side.BOTTOM:
                            adjacentChildIndices[0] = otherChunkChildrenIndex + 2; // bottom left
                            adjacentChildIndices[1] = otherChunkChildrenIndex + 3; // bottom right
                            break;
                        case Side.LEFT:
                            adjacentChildIndices[0] = otherChunkChildrenIndex; // top left
                            adjacentChildIndices[1] = otherChunkChildrenIndex + 2; // bottom left
                            break;
                    }

                    newChunks[borderChildIndices[0]].borders[k] = new ChunkBorder(adjacentChildIndices[0], border.otherSide);
                    newChunks[borderChildIndices[1]].borders[k] = new ChunkBorder(adjacentChildIndices[1], border.otherSide);
                }
            }

            chunks = newChunks;
        }
        return chunks.ToArray();
    }
}

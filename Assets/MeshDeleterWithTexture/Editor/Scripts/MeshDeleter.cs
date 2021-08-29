﻿using Gatosyocora.MeshDeleterWithTexture.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    public static class MeshDeleter
    {
        public static (Mesh, bool[]) RemoveTriangles(Mesh mesh, bool[] deletePos, Vector2Int textureSize, List<int> materialIndexList, bool showProgressBar = true)
        {
            var deletedMesh = UnityEngine.Object.Instantiate(mesh);
            var hadDeletedSubMeshes = new bool[mesh.subMeshCount];

            deletedMesh.Clear();
            deletedMesh.MarkDynamic();

            // 削除する頂点のリストを取得
            var deleteIndexList = GetDeleteVertexIndices(mesh.uv.ToList(), deletePos, textureSize);

            if (!deleteIndexList.Any())
            {
                throw new NotFoundVerticesException("Not found vertices to delete");
            }

            // TODO: 共有されている頂点は存在しない？
            // これがないと他のサブメッシュのポリゴンも削除された
            // 他のサブメッシュで共有されている頂点は削除してはいけない
            List<int> nonDeleteVertexIndexs = new List<int>();
            for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                if (materialIndexList.BinarySearch(subMeshIndex) < 0)
                    nonDeleteVertexIndexs.AddRange(mesh.GetIndices(subMeshIndex));
            }
            nonDeleteVertexIndexs = nonDeleteVertexIndexs.Distinct().ToList();
            nonDeleteVertexIndexs.Sort();

            // 削除する頂点のインデックスのリスト(重複なし)
            var deleteIndexListUnique
                = deleteIndexList
                    .Distinct()
                    .Where(i => nonDeleteVertexIndexs.BinarySearch(i) < 0);

            // 削除する頂点のインデックスのリスト (重複なし, 昇順)
            var deleteIndexsOrdered
                = deleteIndexListUnique
                    .ToList();
            deleteIndexsOrdered.Sort();

            // 削除する頂点がないので終了する
            if (deleteIndexsOrdered.Count == 0) return (mesh, hadDeletedSubMeshes);

            // 頂点を削除
            var nonDeleteVertices = ExtractNonDeleteMeshInfo(mesh.vertices, deleteIndexsOrdered);
            var nonDeleteWeights = ExtractNonDeleteMeshInfo(mesh.boneWeights, deleteIndexsOrdered);
            var nonDeleteNormals = ExtractNonDeleteMeshInfo(mesh.normals, deleteIndexsOrdered);
            var nonDeleteTangents = ExtractNonDeleteMeshInfo(mesh.tangents, deleteIndexsOrdered);
            var nonDeleteColors = ExtractNonDeleteMeshInfo(mesh.colors, deleteIndexsOrdered);
            var nonDeleteColor32s = ExtractNonDeleteMeshInfo(mesh.colors32, deleteIndexsOrdered);
            var nonDeleteUVs = ExtractNonDeleteMeshInfo(mesh.uv, deleteIndexsOrdered);
            var nonDeleteUV2s = ExtractNonDeleteMeshInfo(mesh.uv2, deleteIndexsOrdered);
            var nonDeleteUV3s = ExtractNonDeleteMeshInfo(mesh.uv3, deleteIndexsOrdered);
            var nonDeleteUV4s = ExtractNonDeleteMeshInfo(mesh.uv4, deleteIndexsOrdered);

            deletedMesh.SetVertices(nonDeleteVertices);
            deletedMesh.boneWeights = nonDeleteWeights;
            deletedMesh.SetNormals(nonDeleteNormals);
            deletedMesh.SetTangents(nonDeleteTangents);
            deletedMesh.SetColors(nonDeleteColors);
            deletedMesh.SetColors(nonDeleteColor32s);
            deletedMesh.SetUVs(0, nonDeleteUVs);
            deletedMesh.SetUVs(1, nonDeleteUV2s);
            deletedMesh.SetUVs(2, nonDeleteUV3s);
            deletedMesh.SetUVs(3, nonDeleteUV4s);

            // サブメッシュごとにポリゴンを処理

            // 削除する頂点のインデックスのリスト（重複なし, 降順）
            var deleteIndexListUniqueDescending
                = deleteIndexListUnique
                    .OrderByDescending(value => value)
                    .ToArray();

            // Mesh.GetTrianglesでアクセスするために一旦最大値を入れる
            deletedMesh.subMeshCount = mesh.subMeshCount;

            float progressMaxCount = mesh.subMeshCount;
            float count = 0;
            int addSubMeshIndex = 0;

            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                var subMeshTriangles = mesh.GetTriangles(subMeshIndex);
                // インデックスがずれるので各頂点への対応付けが必要
                // インデックスが大きいものから順に処理していく
                foreach (var deleteVerticesIndex in deleteIndexListUniqueDescending)
                {
                    for (int i = 0; i < subMeshTriangles.Count(); i += 3)
                    {
                        // ポリゴンの3つの頂点1つでも削除されるならそのポリゴンを削除する
                        // mesh.trianglesの要素数は3の倍数である必要がある
                        if (subMeshTriangles[i] == deleteVerticesIndex ||
                            subMeshTriangles[i + 1] == deleteVerticesIndex ||
                            subMeshTriangles[i + 2] == deleteVerticesIndex)
                        {
                            subMeshTriangles[i] = -1;
                            subMeshTriangles[i + 1] = -1;
                            subMeshTriangles[i + 2] = -1;
                        }
                        else
                        {
                            if (subMeshTriangles[i] > deleteVerticesIndex)
                                subMeshTriangles[i]--;
                            if (subMeshTriangles[i + 1] > deleteVerticesIndex)
                                subMeshTriangles[i + 1]--;
                            if (subMeshTriangles[i + 2] > deleteVerticesIndex)
                                subMeshTriangles[i + 2]--;
                        }
                    }
                }

                if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Delete triangles",
                        Mathf.Floor(count / progressMaxCount * 100) + "%", count++ / progressMaxCount))
                {
                    EditorUtility.ClearProgressBar();
                    return (null, Array.Empty<bool>());
                }

                // 不要なポリゴンを削除する
                var triangleList = subMeshTriangles.Where(v => v != -1).ToArray();

                // ポリゴン数0のサブメッシュは追加しない
                if (!triangleList.Any())
                {
                    hadDeletedSubMeshes[subMeshIndex] = true;
                    continue;
                }

                deletedMesh.SetTriangles(triangleList, addSubMeshIndex++);
            }

            EditorUtility.ClearProgressBar();

            if (hadDeletedSubMeshes.Any(deletedSubMesh => deletedSubMesh == true))
            {
                // ポリゴン削除の結果, ポリゴン数0になったSubMeshは含めない
                deletedMesh.subMeshCount = addSubMeshIndex;
            }

            //BindPoseをコピー
            deletedMesh.bindposes = mesh.bindposes;

            // BlendShapeを設定する
            string blendShapeName;
            float frameWeight;
            var deltaVertices = new Vector3[mesh.vertexCount];
            var deltaNormals = new Vector3[mesh.vertexCount];
            var deltaTangents = new Vector3[mesh.vertexCount];
            for (int blendshapeIndex = 0; blendshapeIndex < mesh.blendShapeCount; blendshapeIndex++)
            {
                blendShapeName = mesh.GetBlendShapeName(blendshapeIndex);
                frameWeight = mesh.GetBlendShapeFrameWeight(blendshapeIndex, 0);

                mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);

                var deltaNonDeleteVerteicesList = deltaVertices.Where((value, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
                var deltaNonDeleteNormalsList = deltaNormals.Where((value, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
                var deltaNonDeleteTangentsList = deltaTangents.Where((value, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();

                deletedMesh.AddBlendShapeFrame(blendShapeName, frameWeight,
                    deltaNonDeleteVerteicesList,
                    deltaNonDeleteNormalsList,
                    deltaNonDeleteTangentsList);
            }

            return (deletedMesh, hadDeletedSubMeshes);
        }

        private static List<int> GetDeleteVertexIndices(List<Vector2> uvs, bool[] deletePos, Vector2Int textureSize)
        {
            var deleteIndexList = new List<int>();

            for (int i = 0; i < uvs.Count(); i++)
            {
                var x = (int)(Mathf.Abs(uvs[i].x % 1.0f) * textureSize.x);
                var y = (int)(Mathf.Abs(uvs[i].y % 1.0f) * textureSize.y);

                if (x == textureSize.x || y == textureSize.y) continue;

                int index = y * textureSize.x + x;

                if (deletePos[index])
                {
                    deleteIndexList.Add(i);
                }
            }

            return deleteIndexList;
        }

        private static T[] ExtractNonDeleteMeshInfo<T>(T[] array, List<int> deleteIndexsOrdered)
            => array.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();

    }
}
﻿using Gatosyocora.MeshDeleterWithTexture.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    public class SelectAreaCanvas : MonoBehaviour
    {
        private Material editMat;

        private RenderTexture selectAreaRT;
        private ComputeBuffer buffer;

        private List<Vector4> points;
        private Vector4 latestPoint;
        
        private ComputeShader cs;
        private int addPointKernelId;
        private int addLineKernelId;
        private int fillKernelId;
        private int clearKernelId;

        public SelectAreaCanvas(ref Material editMat)
        {
            this.editMat = editMat;
        }

        public bool SetSelectAreaTexture(Renderer renderer, MaterialInfo materialInfo)
        {
            if (renderer == null || materialInfo == null)
            {
                editMat.SetTexture("_SelectTex", null);
                return true;
            }

            var texture = materialInfo.Texture;

            selectAreaRT = new RenderTexture(texture.width, texture.height, 0)
            {
                enableRandomWrite = true
            };
            selectAreaRT.Create();

            editMat.SetTexture("_SelectTex", selectAreaRT);

            SetupComputeShader(ref selectAreaRT);
            InitalizeProperties();

            return true;
        }

        public void ApplyPenSize(int penSize)
        {
            if (cs == null) return;

            cs.SetInt("PenSize", penSize);
        }

        public void AddSelectAreaPoint(Vector2 pos)
        {
            cs.SetVector("PreviousPoint", latestPoint);

            var point = new Vector4(pos.x, pos.y, 0, 0);
            points.Add(point);
            latestPoint = point;
            cs.SetVector("NewPoint", point);
            cs.Dispatch(addPointKernelId, selectAreaRT.width, selectAreaRT.height, 1);
        }

        public void AddLineEnd2Start()
        {
            DrawLine(points.LastOrDefault(), points.FirstOrDefault());
        }

        public void FillSelectArea()
        {
            var buffer = new ComputeBuffer(points.Count, Marshal.SizeOf(typeof(Vector4)));
            buffer.SetData(points);
            cs.SetBuffer(fillKernelId, "Points", buffer);
            cs.SetInt("PointCount", points.Count);
            cs.Dispatch(fillKernelId, selectAreaRT.width, selectAreaRT.height, 1);

            buffer.Dispose();
        }

        public void ClearSelectArea()
        {
            if (cs == null) return;

            InitalizeProperties();
            cs.Dispatch(clearKernelId, selectAreaRT.width, selectAreaRT.height, 1);
        }

        public bool[] GetFillArea()
        {
            var data = new int[selectAreaRT.width * selectAreaRT.height];
            buffer.GetData(data);

            ClearSelectArea();

            return data.Select(x => x == 1).ToArray();
        }

        private void InitalizeProperties()
        {
            points = new List<Vector4>();
            latestPoint = Vector3.one * -1;
        }

        private void SetupComputeShader(ref RenderTexture renderTexture)
        {
            cs = Object.Instantiate(AssetRepository.LoadCalculateSelectAreaComputeShader());
            addPointKernelId = cs.FindKernel("CSAddPoint");
            addLineKernelId = cs.FindKernel("CSAddLine");
            fillKernelId = cs.FindKernel("CSFill");
            clearKernelId = cs.FindKernel("CSClear");

            cs.SetTexture(addPointKernelId, "SelectAreaTex", renderTexture);
            cs.SetTexture(addLineKernelId, "SelectAreaTex", renderTexture);
            cs.SetTexture(fillKernelId, "SelectAreaTex", renderTexture);
            cs.SetTexture(clearKernelId, "SelectAreaTex", renderTexture);

            buffer = new ComputeBuffer(renderTexture.width * renderTexture.height, sizeof(int));
            cs.SetBuffer(addPointKernelId, "Result", buffer);
            cs.SetBuffer(addLineKernelId, "Result", buffer);
            cs.SetBuffer(fillKernelId, "Result", buffer);
            cs.SetBuffer(clearKernelId, "Result", buffer);

            cs.SetInt("Width", renderTexture.width);
        }

        private void DrawLine(Vector4 a, Vector4 b)
        {
            if (a == null || b == null) return;

            cs.SetVector("Point1", a);
            cs.SetVector("Point2", b);

            cs.Dispatch(addLineKernelId, selectAreaRT.width, selectAreaRT.height, 1);
        }
    }
}

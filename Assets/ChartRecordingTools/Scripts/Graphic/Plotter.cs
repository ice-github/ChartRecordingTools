/**
ChartRecordingTools

Copyright (c) 2017 Sokuhatiku

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Sokuhatiku.ChartRecordingTools
{
	[AddComponentMenu("ChartRecordingTools/Graphic/Plotter")]
	public class Plotter : ChartGraphicBase
	{
		[SerializeField, RecorderDataKey("scope")]
		int dataKey = -1;

		const int MIN_DRAWLIMIT = 6;
		const string SHADER_NAME = "UI/ChartRecorder/Plotter";


		[Header("Plot Option")]
		public float size = 1f;
		public bool drawLine = true;
		public bool cutoffDatalessFrame = true;

		[SerializeField, Range(MIN_DRAWLIMIT, 10000), Header("Load reduction")]
		private int drawsLimit = 100;

        [SerializeField]
        bool mobileMode = false;
        const string SHADER_NAME_FOR_MOBILE = "UI/ChartRecorder/PlotterMobile";

        public struct PointData
		{
			public Vector2 pos;
			public bool drawLine;
		}


		Mesh dummyMesh = null;
		ComputeBuffer buffer = null;
		PointData[] datas = null;

		protected override void UpdateGeometry()
		{
			canvasRenderer.SetMesh(dummyMesh);
		}

		int ptsCount = 0;
		int prevFirst = -1;
		int prevLast = -1;
		protected override void OnUpdateScope()
		{
			var recorder = scope.GetRecorder();
			if (recorder == null) return;

			if (dataKey > -1 &&
				recorder.IsKeyValid(dataKey) &&
				scope.InScopeFirstIndex != -1)
			{

				int first = scope.InScopeFirstIndex;
				int last = scope.InScopeLastIndex;

				// Update Points
				if (prevFirst != first || prevLast != last)
				{
					ptsCount = 0;
					var data = recorder.GetDataReader(dataKey);
					var time = recorder.GetTimeline();

					bool connectPrev = false;

					if (last - first >= drawsLimit)
					{
						// Skip
						int skip = Mathf.NextPowerOfTwo((last - first + drawsLimit - 7) / (drawsLimit - 5));
						var mask = skip - 1;

						first = Mathf.Max(0, first - (first & mask) - skip);
						last = Mathf.Min(time.Count - 1, last - (last & mask) + skip * 2);

						// first point
						if (first == 0)
						{
							AddPoint(time[data.FirstIndex].Value, data[data.FirstIndex], ref connectPrev);
						}

						// skip point
						for (int i = first; i + skip < last; i += skip)
							AddPointAverage(time, data, i, skip, ref connectPrev);

						// last point
						if (last == data.LatestIndex)
						{
							AddPoint(time[data.LatestIndex].Value, data.LatestValue, ref connectPrev);
						}

					}
					else
					{
						var indx = 0;
						try
						{
							// No Skip
							for (int i = first; i <= last; ++i)
							{
								indx = i;
								AddPoint(time[i].Value, data[i], ref connectPrev);
							}
						}
						catch (ArgumentOutOfRangeException)
						{
							Debug.LogErrorFormat("Out of Range!!\ni:{0}, time.count:{1}, data.count:{2}", indx, time.Count, data.Count);
						}
					}

					buffer.SetData(datas);
					prevFirst = scope.InScopeFirstIndex;
					prevLast = scope.InScopeLastIndex;
				}
			}
			else
			{
				ptsCount = 0;
			}

            UpdateMaterialParameters();

            if (mobileMode)
            {
                var scope2local2world = UpdateMaterialParametersForMobile();//TODO: 上記とマージできるかもしれん
                UpdateLineMeshForMobile(scope2local2world);
            }
		}
        protected override void Awake()
        {
            base.Awake();
            InitComponents();
        }

        bool AddPoint(float time, float? data, ref bool connectPrev)
		{
			if (data == null)
			{
				if (cutoffDatalessFrame) connectPrev = false;
				return false;
			}
			var point = new Vector2(time, data.Value);
			datas[ptsCount] = new PointData
			{
				pos = point,
				drawLine = drawLine & connectPrev,
			};
			ptsCount++;
			connectPrev = true;
			return true;
		}

		int AddPointAverage(Data.Reader time, Data.Reader data,
			int start, int dirAndCount, ref bool connectPrev)
		{
			var dir = System.Math.Sign(dirAndCount);
			if (dir == 0) return 0;
			var lim = start + dirAndCount;

			float timeave = 0f;
			float dataave = 0f;
			int datacnt = 0;

			start = Mathf.Clamp(start, 0, data.Count - 1);
			lim = Mathf.Clamp(lim, -1, data.Count);

			for (int i = start; dir > 0 ? i < lim : i > lim; i += dir)
			{
				if (data[i] == null) continue;
				dataave += data[i].Value;
				timeave += time[i].Value;
				++datacnt;
			}

			if (datacnt != 0)
			{
				AddPoint(timeave / datacnt, dataave / datacnt, ref connectPrev);
				return datacnt;
			}
			if (cutoffDatalessFrame)
				connectPrev = false;

			return 0;
		}

		void UpdateMaterialParameters()
		{
			if (material != null)
			{
				material.SetBuffer("Points", buffer);
				material.SetInt("_PointsCount", ptsCount);
				if (ptsCount == 0)
					return;

				material.SetFloat("_Scale", size);
				material.SetColor("_Color", color);

				var scope2Local =
					Matrix4x4.TRS(
						Vector2.Scale(Scope2RectTransration, Scope2RectScale) + Scope2RectOffset,
						Quaternion.identity,
						Scope2RectScale);

				material.SetMatrix("_S2LMatrix", scope2Local);

				var local2World = rectTransform.localToWorldMatrix;
				material.SetMatrix("_L2WMatrix", local2World);

				var maskRect = rectTransform.rect;
				material.SetVector("_ClippingRect", new Vector4(maskRect.xMin, maskRect.yMin, maskRect.xMax, maskRect.yMax));

			}
		}

        void InitComponents()
        {
            if (mobileMode)
            {
                if (GetComponent<MeshFilter>() == null)
                {
                    gameObject.AddComponent<MeshFilter>();
                }
                if (GetComponent<MeshRenderer>() == null)
                {
                    var renderer = gameObject.AddComponent<MeshRenderer>();
                    var shader = Shader.Find(SHADER_NAME_FOR_MOBILE);
                    renderer.material = new Material(shader);
                }

                dummyMesh = null;

                GetComponent<MeshRenderer>().sortingOrder = 1;
            }
            else 
            {
                if (GetComponent<MeshFilter>() != null)
                {
                    DestroyImmediate(GetComponent<MeshFilter>());
                }
                if (GetComponent<MeshRenderer>() != null)
                {
                    DestroyImmediate(GetComponent<MeshRenderer>());
                }
            }
        }

        Matrix4x4 UpdateMaterialParametersForMobile()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return new Matrix4x4();

            var sharedMaterial = renderer.sharedMaterial;

            sharedMaterial.SetColor("_Color", color);

            var scope2Local =
                Matrix4x4.TRS(
                    Vector2.Scale(Scope2RectTransration, Scope2RectScale) + Scope2RectOffset,
                    Quaternion.identity,
                    Scope2RectScale);

            sharedMaterial.SetMatrix("_S2LMatrix", scope2Local);

            var local2World = rectTransform.localToWorldMatrix;
            sharedMaterial.SetMatrix("_L2WMatrix", local2World);

            var maskRect = rectTransform.rect;
            sharedMaterial.SetVector("_ClippingRect", new Vector4(maskRect.xMin, maskRect.yMin, maskRect.xMax, maskRect.yMax));

            renderer.sharedMaterial = sharedMaterial;

            return local2World * scope2Local;
        }

        void UpdateLineMeshForMobile(Matrix4x4 scope2local2world)
        {
            if (datas == null || GetComponent<MeshFilter>() == null) return;

            var triangleList = new System.Collections.Generic.List<int>();
            var vertexList = new System.Collections.Generic.List<Vector3>();

            int vertexIndex = 0;

            for (int i = 2; i < datas.Length - 1; i++)
            {
                if (!datas[i-1].drawLine || !datas[i].drawLine) continue;
                if (datas[i - 1].pos.x > datas[i].pos.x) continue;

                int[] indexes = new int[]
                {
                    vertexIndex + 0, vertexIndex + 2, vertexIndex + 1,
                    vertexIndex + 0, vertexIndex + 3, vertexIndex + 2,
                };
                var vertices = CreateLineVertex(datas[i - 1].pos, datas[i].pos, size/ 25, scope2local2world);

                //追加
                triangleList.AddRange(indexes);
                vertexList.AddRange(vertices);

                vertexIndex += 4;
            }

            if (vertexList.Count == 0 || triangleList.Count == 0) return;

            var filter = GetComponent<MeshFilter>();
            var mesh = new Mesh();
            {
                mesh.vertices = vertexList.ToArray();
                mesh.triangles = triangleList.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
            }
            filter.sharedMesh = mesh;
        }

        Vector3[] CreateLineVertex(Vector2 start, Vector2 end, float width, Matrix4x4 scope2local2world)
        {
            Vector3[] vertices = new Vector3[4];

            //座標変換する
            var startVec4 = scope2local2world * new Vector4(start.x, start.y, 0, 1);
            var endVec4 = scope2local2world * new Vector4(end.x, end.y, 0, 1);

            startVec4 /= startVec4.w;
            endVec4 /= endVec4.w;

            //ベクトルを求める
            var vec = (endVec4 - startVec4).normalized;

            //距離を求める
            var len = (endVec4 - startVec4).magnitude;

            //垂直ベクトルを求めていく

            //v0: 90度回転
            vertices[0].x = startVec4.x - vec.y * width / 2;
            vertices[0].y = startVec4.y + vec.x * width / 2;
            vertices[0].z = startVec4.z;

            //v1: -90度回転
            vertices[1].x = startVec4.x + vec.y * width / 2;
            vertices[1].y = startVec4.y - vec.x * width / 2;
            vertices[1].z = startVec4.z;

            //v2: -90度回転
            vertices[2].x = endVec4.x + vec.y * width / 2;
            vertices[2].y = endVec4.y - vec.x * width / 2;
            vertices[2].z = endVec4.z;

            //v3: 90度回転
            vertices[3].x = endVec4.x - vec.y * width / 2;
            vertices[3].y = endVec4.y + vec.x * width / 2;
            vertices[3].z = endVec4.z;

            //距離が短すぎる場合の対応
            if (len < width && false)
            {
                vertices[0] = vertices[0] + new Vector3(-vec.x, -vec.y, 0) * width;
                vertices[1] = vertices[1] + new Vector3(-vec.x, -vec.y, 0) * width;
                vertices[2] = vertices[2] + new Vector3(vec.x, vec.y, 0) * width;
                vertices[3] = vertices[3] + new Vector3(vec.x, vec.y, 0) * width;
            }

            return vertices;
        }


        void CreateCapasityObject(int capasity)
		{
			dummyMesh = new Mesh();
			var meshvarts = capasity * 3;
			var vertices = new Vector3[meshvarts];
			var indices = new int[meshvarts];
			int i = 0;
			for (; i < meshvarts; i++)
			{
				vertices[i] = new Vector3(i / 2, -i % 2) * 10;
				indices[i] = i;
			}
			dummyMesh.vertices = vertices;
			dummyMesh.SetIndices(indices, MeshTopology.Points, 0);

			buffer = new ComputeBuffer(capasity, Marshal.SizeOf(typeof(PointData)));
			datas = new PointData[capasity];
		}


		protected override void OnEnable()
		{
			base.OnEnable();

			var shader = Shader.Find(SHADER_NAME);
			if (shader != null)
				material = new Material(shader);
			else
			{
				enabled = false;
				return;
			}

			CreateCapasityObject(drawsLimit);
		}

		protected override void OnDisable()
		{
			base.OnDisable();

			if (buffer != null)
			{
				buffer.Release();
				buffer = null;
			}
		}

#if UNITY_EDITOR
		protected override void OnValidate()
		{
			base.OnValidate();

			if (buffer != null && drawsLimit > buffer.count)
			{
				buffer.Dispose();
				CreateCapasityObject(drawsLimit);
			}
		}
#endif
	}
}
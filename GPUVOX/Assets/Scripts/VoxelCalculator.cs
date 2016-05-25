using UnityEngine;
using System.Collections;
//using System.Threading;
using System.Linq;
delegate void CHUNK_OPERATION();

public class VoxelCalculator : Singleton<VoxelCalculator> {
	
	protected VoxelCalculator () {} 
	
	public ComputeShader _CShaderGenerator;
	
	public ComputeShader _CShaderBuilder;
	
	// Maximum buffer size (triangles)
	public int _MaxSize = 21660;

	public int _Trilinear = 1;
	public bool _SmoothNormals = true;
	public int _MultiSampling = 0;
	public float _MSDist = 0.5f;
	public int _WithNeighbours = 1;
	
	//Chunk size in Z dimmension. Used in CS Builder for depth iterations.
	public int _ChunkSizeZ = 27;
		
	public Material _DefaultMaterial;
	
	public int Cycles = 1;

	public Queue QGenerator = new Queue();
	public Queue QBuilder = new Queue();
	
	public int Overlay = 5;
	
	public float AvgTime = 0.0f, MaxTime;

    private int counter = 0;

    private RenderTexture TempRenderTexture;

	public struct Poly
	{
		//Vertex A
		public float A1,A2,A3;
		//Vertex B
		public float B1,B2,B3;
		//Vertex C
		public float C1,C2,C3;
		//Normals
		public float NA1,NA2,NA3;
		public float NB1,NB2,NB3;
		public float NC1,NC2,NC3;
	};
	
	public struct Tris
	{
		//Vertex index A
		public int A;
		//Vertex index B
		public int B;
		//Vertex index C
		public int C;
	};
	
	int[,] m_sampler = new int[,] 
	{
		{1,-1,0}, {1,-1,1}, {0,-1,1}, {-1,-1,1}, {-1,-1,0}, {-1,-1,-1}, {0,-1,-1}, {1,-1,-1}, {0,-1,0},
		{1,0,0}, {1,0,1}, {0,0,1}, {-1,0,1}, {-1,0,0}, {-1,0,-1}, {0,0,-1}, {1,0,-1}, {0,0,0},
		{1,1,0}, {1,1,1}, {0,1,1}, {-1,1,1}, {-1,1,0}, {-1,1,-1}, {0,1,-1}, {1,1,-1}, {0,1,0}
	};
	
	void Start()
	{
		TempRenderTexture = RenderTexture.GetTemporary(_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.sRGB);
		TempRenderTexture.volumeDepth = _ChunkSizeZ+Overlay;
		TempRenderTexture.isVolume = true;
		TempRenderTexture.enableRandomWrite = true;
		TempRenderTexture.filterMode = FilterMode.Point;
		TempRenderTexture.wrapMode = TextureWrapMode.Clamp;
		TempRenderTexture.Create();
		
		CreateEmptyVolume(TempRenderTexture,_ChunkSizeZ+Overlay);
	}
	
	void Update()
	{
		
		if (QGenerator.Count > 0)
		{
			for (int n=0; n<QGenerator.Count;n++)
			{
				CHUNK_OPERATION Operation = QGenerator.Dequeue() as CHUNK_OPERATION;
				Operation();
			}	
		}
		else
		if (QBuilder.Count > 0)
		{
			for (int n=0; n<QBuilder.Count;n++)
			{
				if (counter < Cycles){
					CHUNK_OPERATION Operation = QBuilder.Dequeue() as CHUNK_OPERATION;
					Operation();
					counter++;
				}
			}		
		}
		counter = 0;
		
	}
	
	new void OnDestroy()
	{
		TempRenderTexture.Release();
	}
	
	public RenderTexture[,,] GetNeighbours(VoxelChunk chunk)
	{
		RenderTexture[,,] Neighbours = new RenderTexture[3,3,3];
		Transform ChunkContainer = chunk.transform.parent;
		if (ChunkContainer!=null){
			int n=0;
			for(int i = 0; i < 27; i++)
			{
				Vector3 Pos = new Vector3(0,0,0)+chunk.transform.position+chunk.transform.right*(chunk._SizeZ/2.0f) + chunk.transform.up*(chunk._SizeZ/2.0f) + chunk.transform.forward*(chunk._SizeZ/2.0f);
				
				Vector3 NewCoord = new Vector3(0,0,0)+chunk.transform.position + chunk.transform.right*m_sampler[i,0]*(chunk._SizeZ) + chunk.transform.up*m_sampler[i,1]*(chunk._SizeZ) + chunk.transform.forward*m_sampler[i,2]*(chunk._SizeZ);
	
				Vector3 vCenter = NewCoord+chunk.transform.right*(chunk._SizeZ/2.0f) + chunk.transform.up*(chunk._SizeZ/2.0f) + chunk.transform.forward*(chunk._SizeZ/2.0f);
				
				/*if (m_sampler[i,0]+1 == 2 && m_sampler[i,1]+1 == 1 && m_sampler[i,2]+1 == 1)
					Debug.DrawLine(vCenter, Pos, Color.red, 30.0f);
				if (m_sampler[i,0]+1 == 1 && m_sampler[i,1]+1 == 2 && m_sampler[i,2]+1 == 1)
					Debug.DrawLine(vCenter, Pos, Color.green, 30.0f);
				if (m_sampler[i,0]+1 == 1 && m_sampler[i,1]+1 == 1 && m_sampler[i,2]+1 == 2)
					Debug.DrawLine(vCenter, Pos, Color.blue, 30.0f);
				*/
							
				//Debug.DrawLine(NewCoord+new Vector3(Size/2,Size/2,Size/2), Point, Color.red);
				
				VoxelChunk Neighbour = GetChunk(ChunkContainer, NewCoord, false, chunk._SizeZ);
				
				if (Neighbour!=null && Neighbour.DensityVolume != null){
					Neighbours[m_sampler[i,0]+1,m_sampler[i,1]+1,m_sampler[i,2]+1] = Neighbour.DensityVolume;
					n++;
					//chunk.DrawVoxel(Color.yellow,NewCoord,3.0f);
				}
				//else
				//	Debug.DrawLine(vCenter, Pos, Color.red,3.0f);
				
					
								
			}
		}
		//Debug.Log("n = "+n);
		return Neighbours;
	}
	
	public void CreateEmptyVolume(RenderTexture Volume, int iSize = 32)
	{
		if (Volume == null)
		{
			Debug.LogWarning("NULL Voulme, generation aborted");
			return;
		}
		int mgen_id = _CShaderGenerator.FindKernel("FillEmpty");
		_CShaderGenerator.SetTexture(mgen_id,"Result",Volume);
		_CShaderGenerator.Dispatch(mgen_id,iSize+Overlay,iSize+Overlay,iSize+Overlay);
		
	}
	public void FillBorders(RenderTexture Volume, RenderTexture[,,] Neighbours, int iSize = 32)
	{
		if (Neighbours.GetLength(0) == 0){
			Neighbours = new RenderTexture[3,3,3];
			Debug.LogWarning("Empty neighbourhood");
		}
		if (Volume == null)
		{
			Debug.LogWarning("NULL Voulme, generation aborted");
			return;
		}
		Debug.Log("FillBorders");
		int mgen_id = _CShaderGenerator.FindKernel("FillBorders");

		RenderTexture TempRenderTexture = RenderTexture.GetTemporary(iSize,iSize,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.sRGB);
		TempRenderTexture.volumeDepth = iSize;
		TempRenderTexture.isVolume = true;
		TempRenderTexture.enableRandomWrite = true;
		TempRenderTexture.filterMode = FilterMode.Point;
		TempRenderTexture.wrapMode = TextureWrapMode.Clamp;
		TempRenderTexture.Create();
		//_CShaderGenerator.SetTexture(mgen_id,"Result",TempRenderTexture);
		//_CShaderGenerator.Dispatch(mgen_id,1,1,iSize);
		
		
		_CShaderGenerator.SetTexture(mgen_id,"Result",Volume);
		_CShaderGenerator.SetTexture(mgen_id,"vol100",Neighbours[2,1,1]!=null?Neighbours[2,1,1]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol010",Neighbours[1,2,1]!=null?Neighbours[1,2,1]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol001",Neighbours[1,1,2]!=null?Neighbours[1,1,2]:TempRenderTexture);
		
		_CShaderGenerator.SetTexture(mgen_id,"volm100",Neighbours[0,1,1]!=null?Neighbours[0,1,1]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol0m10",Neighbours[1,0,1]!=null?Neighbours[1,0,1]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol00m1",Neighbours[1,1,0]!=null?Neighbours[1,1,0]:TempRenderTexture);
		 
		_CShaderGenerator.Dispatch(mgen_id,iSize,iSize,iSize);
		
		TempRenderTexture.Release();
		
	}
	
	public void CreateNoiseVolume(RenderTexture Volume, Vector3 Pos, int iSize = 32, float Str = 40.0f, float NoiseA = 0.000718f, float NoiseB = 0.000632f, float NoiseC = 0.000695f)
	{
		CHUNK_OPERATION d;
		d = delegate() 
		{
			
			if (Volume == null)
			{
				Debug.LogWarning("NULL Voulme, generation aborted");
				return;
			}
			
			float startTime = Time.realtimeSinceStartup;
			
			int mgen_id = _CShaderGenerator.FindKernel("Simplex3d");
	
			_CShaderGenerator.SetTexture(mgen_id,"Result",Volume);
			
			_CShaderGenerator.SetVector("_StartPos",new Vector4(Pos.x,Pos.y,Pos.z,0.0f));
			//_CShaderGenerator.SetFloat("_MyTime",Time.time*Speed);
			_CShaderGenerator.SetFloat("_Str",Str);
			_CShaderGenerator.SetFloat("_NoiseA",NoiseA);
			_CShaderGenerator.SetFloat("_NoiseB",NoiseB);
			_CShaderGenerator.SetFloat("_NoiseC",NoiseC);
			
			_CShaderGenerator.Dispatch(mgen_id,iSize,iSize,iSize);
			//Debug.Log("Noise generation time:  " + (1000.0f*(Time.realtimeSinceStartup-startTime)).ToString()+"ms");
		};
		QGenerator.Enqueue(d);
	}
	
	public void CreateSphereVolume(RenderTexture Volume, Vector3 Pos)
	{
		CHUNK_OPERATION d;
		d = delegate() 
		{
			if (Volume == null)
			{
				Debug.LogWarning("NULL Voulme, generation aborted");
				return;
			}
			
			float startTime = Time.realtimeSinceStartup;
			
			int mgen_id = _CShaderGenerator.FindKernel("Sphere");
	
			_CShaderGenerator.SetTexture(mgen_id,"Result",Volume);
			
			_CShaderGenerator.SetVector("_StartPos",new Vector4(Pos.x,Pos.y,Pos.z,0.0f));
	
			_CShaderGenerator.Dispatch(mgen_id,_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay);
			//Debug.Log("Sphere generation time:  " + (1000.0f*(Time.realtimeSinceStartup-startTime)).ToString()+"ms");
		};
		QGenerator.Enqueue(d);
	}
	
	public void CreatePlaneVolume(RenderTexture Volume,Vector3 Pos)
	{
		if (Volume == null)
		{
			Debug.LogWarning("NULL Voulme, generation aborted");
			return;
		}
		
		int mgen_id = _CShaderGenerator.FindKernel("Plane");

		_CShaderGenerator.SetTexture(mgen_id,"Result",Volume);
		
		_CShaderGenerator.SetVector("_StartPos",new Vector4(Pos.x,Pos.y,Pos.z,0.0f));
		
		_CShaderGenerator.Dispatch(mgen_id,_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay);
		//Debug.Log("Sphere generation time:  " + (1000.0f*(Time.realtimeSinceStartup-startTime)).ToString()+"ms");
	}
	
	public void BuildChunkMesh(RenderTexture Volume, Mesh NewMesh, RenderTexture[,,] Neighbours)
	{
		if (Volume == null || NewMesh == null)
		{
			Debug.LogWarning("Can't build mesh '"+NewMesh+"' from '"+Volume+"' volume");
			return;
		}
		
		float startTime = Time.realtimeSinceStartup;
		
		
		
		RenderTexture IDVol = RenderTexture.GetTemporary(_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay,0,RenderTextureFormat.RInt,RenderTextureReadWrite.sRGB);
		IDVol.volumeDepth = (_ChunkSizeZ+Overlay)*3;
		IDVol.isVolume = true;
		IDVol.enableRandomWrite = true;
		IDVol.filterMode = FilterMode.Point;
		IDVol.wrapMode = TextureWrapMode.Clamp;
		IDVol.Create();
		
		
		//int[] index_pool = new int[ 4 ];
		Vector3[] vertex_buffer = new Vector3[_MaxSize*3];
		int[] triangles_buffer = new int[_MaxSize*3];
		int[] cntbuffer = new int[2];
		
		//ComputeBuffer IndexPool = new ComputeBuffer(4,4);
		ComputeBuffer VertexBuffer = new ComputeBuffer(_MaxSize,36);
		ComputeBuffer TrisBuffer = new ComputeBuffer(_MaxSize,12);
		ComputeBuffer CountBuffer = new ComputeBuffer(2,4);
				
		//Set data to container
		//IndexPool.SetData(index_pool);
		VertexBuffer.SetData(vertex_buffer);
		TrisBuffer.SetData(triangles_buffer);
		CountBuffer.SetData(cntbuffer);
		
		//Set parameters for building
		_CShaderBuilder.SetInt("_Trilinear",_Trilinear);
		_CShaderBuilder.SetInt("_Size",_ChunkSizeZ-Overlay);
		_CShaderBuilder.SetInt("_MultiSampling",_MultiSampling);
		_CShaderBuilder.SetFloat("_MSDist",_MSDist);
		_CShaderBuilder.SetInt("_WithNeighbours", _WithNeighbours);
		
		int id = _CShaderBuilder.FindKernel("BuildVertices");
		
		
		/*RenderTexture Vol = new RenderTexture(_ChunkSizeZ+2,_ChunkSizeZ+2,24,RenderTextureFormat.RFloat,RenderTextureReadWrite.sRGB);
		
		Vol.volumeDepth = _ChunkSizeZ+2;
		Vol.isVolume = true;
		Vol.enableRandomWrite = true;
		
		Vol.wrapMode = TextureWrapMode.Clamp;
		Vol.filterMode = FilterMode.Bilinear;
		Vol.useMipMap = true;
		Vol.mipMapBias=0.0f;
		Vol.Create();
		Graphics.Blit(Volume,Vol);*/
		
		_CShaderBuilder.SetTexture(id,"_ivolume", Volume);
		
		_CShaderBuilder.SetTexture(id,"index_volume", IDVol);
		
		if (_WithNeighbours != 0){
			/*if (Neighbours.GetLength(0) == 0)
				Neighbours = new RenderTexture[3,3,3];
			else{
				int n = 0;
				foreach (RenderTexture N in Neighbours)
				{
					if (N != null)	
						n++;
				}
				Debug.Log(n+" neighbours");
			}
			_CShaderBuilder.SetTexture(id,"vol100",Neighbours[2,1,1]!=null?Neighbours[2,1,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol010",Neighbours[1,2,1]!=null?Neighbours[1,2,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol001",Neighbours[1,1,2]!=null?Neighbours[1,1,2]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"volm100",Neighbours[0,1,1]!=null?Neighbours[0,1,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol0m10",Neighbours[1,0,1]!=null?Neighbours[1,0,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol00m1",Neighbours[1,1,0]!=null?Neighbours[1,1,0]:TempRenderTexture);
			*/
			FillBorders(Volume,Neighbours,32);
		}
		//else
		//	Debug.Log("no Neighbours");
		 
		//_CShaderBuilder.SetBuffer(id,"index_pool", IndexPool);
		_CShaderBuilder.SetBuffer(id,"vertex_buffer", VertexBuffer);
		_CShaderBuilder.SetBuffer(id,"count_buffer", CountBuffer);
		
		// ========== Build VERTICES ==========
		_CShaderBuilder.Dispatch(id,1,1,1);
		// ====================================

		//Recieve data from container
		//IndexPool.GetData(index_pool);
		VertexBuffer.GetData(vertex_buffer);
		CountBuffer.GetData(cntbuffer);
		
		
		id = _CShaderBuilder.FindKernel("BuildTriangles");
		_CShaderBuilder.SetTexture(id,"_ivolume", Volume);
		_CShaderBuilder.SetTexture(id,"index_volume", IDVol);
		
		/*if (_WithNeighbours != 0){
			_CShaderBuilder.SetTexture(id,"vol100",Neighbours[2,1,1]!=null?Neighbours[2,1,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol010",Neighbours[1,2,1]!=null?Neighbours[1,2,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol001",Neighbours[1,1,2]!=null?Neighbours[1,1,2]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"volm100",Neighbours[0,1,1]!=null?Neighbours[0,1,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol0m10",Neighbours[1,0,1]!=null?Neighbours[1,0,1]:TempRenderTexture);
			_CShaderBuilder.SetTexture(id,"vol00m1",Neighbours[1,1,0]!=null?Neighbours[1,1,0]:TempRenderTexture);
		}*/
		
		//_CShaderBuilder.SetBuffer(id,"index_pool", IndexPool);
		_CShaderBuilder.SetBuffer(id,"triangles_buffer", TrisBuffer);
		_CShaderBuilder.SetBuffer(id,"count_buffer", CountBuffer);
		
		_CShaderBuilder.SetInt("_Size",_ChunkSizeZ-Overlay-1);
		// ========== Build TRIANGLES ==========
		_CShaderBuilder.Dispatch(id,1,1,1);
		// =====================================

		//VertexBuffer.GetData(vertex_buffer);
		TrisBuffer.GetData(triangles_buffer);
		
		//IndexPool.GetData(index_pool);
		CountBuffer.GetData(cntbuffer);
		//IDVol.Release();
		
		float T = 1000.0f*(Time.realtimeSinceStartup-startTime);
		AvgTime = (AvgTime+T)/2.0f;
		if (T < 50)
			MaxTime = Mathf.Max(MaxTime,T);
		//Debug.Log("Building time: " + (1000.0f*(Time.realtimeSinceStartup-startTime)).ToString()+"ms");
		
		/*for (int ni = 0; ni < 128; ni++){
			Poly P = vertex_buffer[ni];
			Debug.Log(ni+") P.A ="+new Vector3(vertex_buffer[ni].A1,vertex_buffer[ni].A2,vertex_buffer[ni].A3) );
			Debug.Log(ni+") P.B ="+new Vector3(vertex_buffer[ni].B1,vertex_buffer[ni].B2,vertex_buffer[ni].B3) );
			Debug.Log(ni+") P.C ="+new Vector3(vertex_buffer[ni].C1,vertex_buffer[ni].C2,vertex_buffer[ni].C3) );
			
		}*/
		/*for (int ni = 0; ni < 128; ni++){
			Vector3 vp = vertex_buffer[ni];
			if (vp.x != 0 || vp.y != 0 || vp.z != 0)
				Debug.Log(ni+") VPOOL ="+vp);
		}*/
		
		//Construct mesh using received data
		
		int vindex = 0;
				
		/*for (count=0;count<_MaxSize*3; count++)
		{
			if (
				//vertex_buffer[triangles_buffer[count-2]] == Vector3.zero && 
				//vertex_buffer[triangles_buffer[count-1]] == Vector3.zero && 
				vertex_buffer[triangles_buffer[count]].x == 0 && vertex_buffer[triangles_buffer[count]].y == 0 && vertex_buffer[triangles_buffer[count]].z == 0
				)
			{
				break;
			}
		}*/
		int vcount = cntbuffer[0]+1; //vertex_buffer.Length;//index_pool[131073];
		//Debug.Log(vcount+" vertices got");
		int count = cntbuffer[1]+1;//triangles_buffer.Length;//index_pool[131072];
		//Debug.Log(count+" triangles got");
		

		//We have got all data and are ready to setup a new mesh!
		
		NewMesh.Clear();
		
		Vector3[] vertices = new Vector3[vcount];
		int[] tris = new int[(count+1)*3];
		Vector3[] normals = new Vector3[vcount];
		/*for (int i=0;i<count*3;i++)
		{
			tris[i] = triangles_buffer[i];
			//vertices[tris[i]] = vertex_buffer[tris[i]];
			i++;
		}*/
		vertices = vertex_buffer.Take(vcount).ToArray();
		NewMesh.vertices = vertices; //vertex_buffer;//
		//NewMesh.uv = uvs; //Unwrapping.GeneratePerTriangleUV(NewMesh);
		tris = triangles_buffer.Take((count)*3).ToArray();
		NewMesh.triangles = tris; //triangles_buffer
		
		normals = vertex_buffer.Skip(32000).Take(vcount).ToArray();
		
		if (_SmoothNormals)
		{
			NewMesh.normals = normals; 
		}
		else
			NewMesh.RecalculateNormals();
		
		NewMesh.Optimize();
		
		VertexBuffer.Dispose();
		TrisBuffer.Dispose();
		CountBuffer.Dispose();
		RenderTexture.ReleaseTemporary(IDVol);
		//Vol.Release();
	}
	
	public VoxelChunk NewChunk(Transform ChunkContainer, Vector3 vPos, int iSize = 27)
	{
		GameObject ChunkObject = new GameObject(vPos.ToString(),typeof(MeshRenderer), typeof(MeshFilter),typeof(MeshCollider), typeof(VoxelChunk));
		ChunkObject.transform.parent = ChunkContainer;
		ChunkObject.GetComponent<MeshRenderer>().material =  _DefaultMaterial;
		ChunkObject.transform.localScale = ChunkContainer.localScale;
		ChunkObject.transform.localPosition = vPos;
		ChunkObject.transform.rotation = ChunkContainer.rotation;
		
		VoxelChunk Chunk = ChunkObject.GetComponent<VoxelChunk>();
		Chunk._SizeZ = iSize;
		Chunk.Init();
		//RenderTexture[,,] Neighbours = GetNeighbours(Chunk);
		//CreateEmptyVolume(Chunk.DensityVolume, Neighbours, iSize);
		//CreateEmptyVolume(Chunk.DensityVolume,32);
		return Chunk;
	}
	
	
	VoxelChunk GetChunk(Transform ChunkContainer, Vector3 vPos, bool bCreate, int iSize = 27)
	{
		Transform ChunkTransform;
		vPos = ChunkContainer.InverseTransformPoint(vPos)*ChunkContainer.localScale.x;
		/*Vector3 vINT;
		vINT.x = Mathf.Clamp(Mathf.RoundToInt(pPos.x/Size),-1,1);
		vINT.y = Mathf.Clamp(Mathf.RoundToInt(pPos.y/Size),-1,1);
		vINT.z = Mathf.Clamp(Mathf.RoundToInt(pPos.z/Size),-1,1);
		*/		
		ChunkTransform = ChunkContainer.FindChild(vPos.ToString());
		
		if (ChunkTransform == null && bCreate)
		{
			//Debug.Log("Create NEW Chunk");
			/*GameObject ChunkObject = new GameObject(pPos.ToString(),typeof(MeshRenderer), typeof(MeshFilter),typeof(MeshCollider), typeof(VChunk));
			ChunkObject.transform.parent = transform;
			ChunkObject.GetComponent<MeshRenderer>().material =  _material;
			ChunkObject.transform.localScale = Scale;
			ChunkObject.transform.localPosition = pPos;
			*/
			VoxelChunk c = NewChunk(ChunkContainer, vPos, iSize);
			if (c == null)
				Debug.LogWarning("NULL creation!");
			return c;

		}
		else{
			//Debug.Log("Get Chunk "+ChunkTransform.GetComponent<VChunk>());
			if (ChunkTransform == null)
				return null;
			return ChunkTransform.GetComponent<VoxelChunk>();
		}
	}
	
	
	public void PaintBrushAll(/*Transform ChunkContainer*/VoxelChunk chunk, Vector3 Pos, float fSize, float fStr)
	{
		if (chunk.transform.parent != null)
		{
			Transform ChunkContainer = chunk.transform.parent;
			for(int i = 0; i < 27; i++)
			{
				
				Vector3 NewCoord = chunk.transform.position + chunk.transform.right*m_sampler[i,0]*(chunk._SizeZ) + chunk.transform.up*m_sampler[i,1]*(chunk._SizeZ) + chunk.transform.forward*m_sampler[i,2]*(chunk._SizeZ);
				//chunk.DrawVoxel(Color.yellow, NewCoord);
		
				//ChunkHelper.DrawVoxel(Color.yellow, NewCoord, transform.rotation, Size);
		
				//Debug.Log( "NewCoord = "+( transform.InverseTransformPoint(NewCoord)*transform.localScale.x ) );
				Vector3 vCenter = NewCoord+chunk.transform.right*(chunk._SizeZ/2.0f) + chunk.transform.up*(chunk._SizeZ/2.0f) + chunk.transform.forward*(chunk._SizeZ/2.0f);
				Debug.DrawLine(Pos, vCenter, Color.yellow);
				
				if ( Vector3.Distance(vCenter, Pos) <= Mathf.Clamp(Mathf.Abs( (chunk._SizeZ/1.3f)+fSize*2.0f),0,chunk._SizeZ*2.0f) )
				{
					
					//Debug.DrawLine(NewCoord+new Vector3(Size/2,Size/2,Size/2), Point, Color.red);
					
					VoxelChunk Neighbour = GetChunk(ChunkContainer, NewCoord, true, chunk._SizeZ);
					if (Neighbour == null)
						Debug.LogWarning("GOT NULL CHUNK!");
					//Neighbour.DrawVoxel(Color.yellow,Neighbour.transform.position);
					PaintBrush(Neighbour, Pos, fSize, fStr);
					Neighbour.bActual = true;
				}
	
			}
		}
		else
			PaintBrush(chunk, Pos, fSize, fStr);
		
		/*
		RenderTexture TempRenderTexture = RenderTexture.GetTemporary(_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.sRGB);
		TempRenderTexture.volumeDepth = _ChunkSizeZ+Overlay;
		TempRenderTexture.isVolume = true;
		TempRenderTexture.enableRandomWrite = true;
		TempRenderTexture.filterMode = FilterMode.Point;
		TempRenderTexture.wrapMode = TextureWrapMode.Clamp;
		TempRenderTexture.Create();
		CreateEmptyVolume(TempRenderTexture,_ChunkSizeZ+Overlay);
		
		RenderTexture[,,] Neighbours = GetNeighbours(chunk);
		
		Vector3 LocalPos = chunk.transform.InverseTransformPoint(Pos)*chunk.transform.localScale.x;

		Vector3 pPos = new Vector4(LocalPos.x,LocalPos.y,LocalPos.z,0.0f);
	
		int mgen_id = _CShaderGenerator.FindKernel("SphereAdd");

		_CShaderGenerator.SetTexture(mgen_id,"vol0",Neighbours[0]!=null?Neighbours[0]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol1",Neighbours[1]!=null?Neighbours[1]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol2",Neighbours[2]!=null?Neighbours[2]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol3",Neighbours[3]!=null?Neighbours[3]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol4",Neighbours[4]!=null?Neighbours[4]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol5",Neighbours[5]!=null?Neighbours[5]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol6",Neighbours[6]!=null?Neighbours[6]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol7",Neighbours[7]!=null?Neighbours[7]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol8",Neighbours[8]!=null?Neighbours[8]:TempRenderTexture);
		
		_CShaderGenerator.SetTexture(mgen_id,"vol9",Neighbours[9]!=null?Neighbours[9]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol10",Neighbours[10]!=null?Neighbours[10]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol11",Neighbours[11]!=null?Neighbours[11]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol12",Neighbours[12]!=null?Neighbours[12]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol13",Neighbours[13]!=null?Neighbours[13]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol14",Neighbours[14]!=null?Neighbours[14]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol15",Neighbours[15]!=null?Neighbours[15]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol16",Neighbours[16]!=null?Neighbours[16]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol17",Neighbours[17]!=null?Neighbours[17]:TempRenderTexture);
		
		_CShaderGenerator.SetTexture(mgen_id,"vol18",Neighbours[18]!=null?Neighbours[18]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol19",Neighbours[19]!=null?Neighbours[19]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol20",Neighbours[20]!=null?Neighbours[20]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol21",Neighbours[21]!=null?Neighbours[21]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol22",Neighbours[22]!=null?Neighbours[22]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol23",Neighbours[23]!=null?Neighbours[23]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol24",Neighbours[24]!=null?Neighbours[24]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol25",Neighbours[25]!=null?Neighbours[25]:TempRenderTexture);
		_CShaderGenerator.SetTexture(mgen_id,"vol26",Neighbours[26]!=null?Neighbours[26]:TempRenderTexture);
		
		_CShaderGenerator.SetVector("_BrushPos",pPos);
		_CShaderGenerator.SetFloat("_Brush_Size",fSize);
		_CShaderGenerator.SetFloat("_Brush_Strength",fStr);
		
		_CShaderGenerator.Dispatch(mgen_id,_ChunkSizeZ*3,_ChunkSizeZ*3,_ChunkSizeZ*3);
		
		TempRenderTexture.Release();
		*/
	}
	
	public void PaintBrush(VoxelChunk chunk, Vector3 Pos, float fSize, float fStr)
	{
		if (chunk == null || chunk.DensityVolume == null)
		{
			Debug.LogWarning("invaild chunk");
			return;
		}
	
		//Debug.Log("PaintBrush");
		//Chunk.FillEmpty(CShader_Generator,Chunk.PersistentVol);
	
		//Debug.DrawLine(chunk.transform.position, Pos,Color.red);
	
		/*CHUNK_OPERATION d;
		d = delegate() 
		{
		*/
		CHUNK_OPERATION d;
		chunk.cColor = Color.red;
		
		if (!chunk.QPlanned){
			//CHUNK_OPERATION d;
			chunk.QPlanned = true;
			d = delegate() 
			{
				if (chunk == null)
				{
					Debug.LogWarning("chunk was already destroyed");
					return;
				}
				Mesh ChunkMesh = chunk.transform.GetComponent<MeshFilter>().sharedMesh;
				if ( _WithNeighbours !=0 ){
					RenderTexture[,,] Neighbours = GetNeighbours(chunk);
					BuildChunkMesh(chunk.DensityVolume,ChunkMesh,Neighbours);
				}
				else
					BuildChunkMesh(chunk.DensityVolume,ChunkMesh,null);
				
				chunk._Col = Color.green;
				chunk.bNeedCollisionUpdate = true;
				chunk.QPlanned = false;
				
				//CHUNK_OPERATION d2;
				//d2 = delegate()
				//{
				if (chunk.IsInvoking())
					chunk.CancelInvoke();
				chunk.Invoke("CheckNeedToDestroy",3.0f);	
				//};
				//QBuilder.Enqueue(d2);
				//CreateEmptyVolume(chunk.DensityVolume, GetNeighbours(chunk), chunk._SizeZ);
			};
			QBuilder.Enqueue(d);
			
		}
		
		if (!chunk.QDPlanned){
			chunk.QDPlanned = true;
			d = delegate() 
			{
				Vector3 LocalPos = chunk.transform.InverseTransformPoint(Pos)*chunk.transform.localScale.x;
				//Debug.DrawLine(chunk.transform.position, chunk.transform.position+LocalPos,Color.yellow);
				
				//Vector3 cPos = new Vector4(chunk.transform.localPosition.x,chunk.transform.localPosition.y,chunk.transform.localPosition.z,0.0f);
				Vector3 pPos = new Vector4(LocalPos.x,LocalPos.y,LocalPos.z,0.0f);
			
				int mgen_id = _CShaderGenerator.FindKernel("SphereAdd");
					
				_CShaderGenerator.SetTexture(mgen_id,"Result",chunk.DensityVolume);
				//RenderTexture[] vol = new RenderTexture[27];
				//vol[0] = chunk.DensityVolume;
				
				
				//_CShaderGenerator.SetVector("_StartPos",new Vector4(cPos.x,cPos.y,cPos.z,0.0f));
				_CShaderGenerator.SetVector("_BrushPos",pPos);
				_CShaderGenerator.SetFloat("_Brush_Size",fSize);
				_CShaderGenerator.SetFloat("_Brush_Strength",fStr);
				
				_CShaderGenerator.Dispatch(mgen_id,_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay,_ChunkSizeZ+Overlay);
				
				//chunk.cColor = Color.yellow;
				chunk._Col = Color.green;
				chunk.QDPlanned = false;
			};
			QGenerator.Enqueue(d);
			
		}
		
	}
}

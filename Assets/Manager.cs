using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Text;

public enum State
{
	Start,
	Converting,
	Done,
}

public enum ConversionModes
{
	Default,
	NoUVs,
	NoUVsAndNoGroups,
}

[Flags]
public enum Flags
{
	None      = 0,
	UV        = 1,  // 0x01
	Normals   = 2,  // 0x02
	Flexible  = 4,  // 0x08
	Unknown8  = 8,  // 0x04
	Unknown16 = 16, // 0x10
	Unknown32 = 32, // 0x20
}

public class Manager : MonoBehaviour
{
	// inspector
	public Material brickMaterial;
	
	// general
	State state;
	string[] inputFiles;
	int currentFile = 0;
	DateTime startTime;
	TimeSpan timeSpan;
	
	// user input
	string inputDirectory = @"C:\Some\Directory";
	string outputDirectory = @"C:\Another\Directory";
	ConversionModes conversionMode;
	
	// reset these per frame/brick
	List<Mesh> meshes = new List<Mesh>();
	List<GameObject> meshGameObjects = new List<GameObject>();
	string brickID;
	bool brickHasUVs = false;
	
	void Start()
	{
		if (PlayerPrefs.HasKey("Input Directory"))
		{
			inputDirectory = PlayerPrefs.GetString("Input Directory");
		}
		if (PlayerPrefs.HasKey("Output Directory"))
		{
			outputDirectory = PlayerPrefs.GetString("Output Directory");
		}
		if (PlayerPrefs.HasKey("Conversion Mode"))
		{
			conversionMode = (ConversionModes)PlayerPrefs.GetInt("Conversion Mode");
		}
	}
	
	void OnGUI()
	{
		switch (state)
		{
			case State.Start:
				GUI.Box(new Rect(10, 10, 250, 220), "");
				
				GUI.Label(new Rect(15, 10, 240, 25), "Input directory:");
				inputDirectory = GUI.TextField(new Rect(15, 30, 240, 25), inputDirectory, 100);
				
				GUI.Label(new Rect(15, 60, 240, 25), "Output directory:");
				outputDirectory = GUI.TextField(new Rect(15, 80, 240, 25), outputDirectory, 100);
				
				GUI.Label(new Rect(15, 110, 240, 25), "Conversion mode:");
				string[] conversionModeOptions = new string[] {" Default", " No UVs", " No UVs, no groups"};
				conversionMode = (ConversionModes)GUI.SelectionGrid(new Rect(15, 130, 240, 62), (int)conversionMode, conversionModeOptions, 1, "toggle");
				
				if (GUI.Button(new Rect(15, 200, 240, 25), "Go"))
				{
					if (!Directory.Exists(inputDirectory))
					{
						inputDirectory = "Directory doesn't exist";
						break;
					}
					inputFiles = Directory.GetFiles(inputDirectory, "*.g");
					//Debug.Log(inputFiles.Length + " bricks");
					if (inputFiles.Length == 0)
					{
						inputDirectory = "No .g files found";
						break;
					}
					if (!Directory.Exists(outputDirectory))
					{
						Directory.CreateDirectory(outputDirectory);
					}
					PlayerPrefs.SetString("Input Directory", inputDirectory);
					PlayerPrefs.SetString("Output Directory", outputDirectory);
					PlayerPrefs.SetInt("Conversion Mode", (int)conversionMode);
					QualitySettings.vSyncCount = 0; // disable vsync while converting so we don't limit conversion speed to framerate
					startTime = DateTime.Now;
					state = State.Converting;
				}
				break;
			case State.Converting:
				GUI.Box(new Rect(10, 10, 250, 70), "");
				GUI.Label(new Rect(15, 10, 240, 25), string.Format("Converted brick {0} ({1} of {2})", brickID, currentFile, inputFiles.Length));
				GUI.Label(new Rect(15, 30, 240, 25), string.Format("Time: {0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
				
				string nextBrickID = Path.GetFileNameWithoutExtension(inputFiles[currentFile] + 1);
				GUI.Label(new Rect(15, 50, 240, 25), string.Format("Now converting brick {0}...", nextBrickID));
				break;
			case State.Done:
				GUI.Box(new Rect(10, 10, 250, 70), "");
				GUI.Label(new Rect(15, 10, 240, 25), string.Format("Converted brick {0} ({1} of {2})", brickID, currentFile, inputFiles.Length));
				GUI.Label(new Rect(15, 30, 240, 25), string.Format("Time: {0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
				
				if (GUI.Button(new Rect(15, 50, 240, 25), "Back"))
				{
					SceneManager.LoadScene("Scene");
				}
				break;
		}
	}
	
	void Update()
	{
		if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.R))
		{
			Debug.Log("Clearing PlayerPrefs");
			PlayerPrefs.DeleteAll();
		}
		
		if (state == State.Converting && currentFile < inputFiles.Length)
		{
			// RESET FROM LAST FRAME
			
			// nuke the bricks in the scene
			// dunno if destroying the meshes on their meshfilters is redundant with destroying the ones in the meshes list but whatever
			foreach (GameObject gameObjectToNuke in meshGameObjects)
			{
				Destroy(gameObjectToNuke.GetComponent<MeshFilter>().mesh);
				Destroy(gameObjectToNuke);
			}
			meshGameObjects.Clear();
			
			// shrug
			foreach (Mesh mesh in meshes)
			{
				Destroy(mesh);
			}
			meshes.Clear();
			
			// pls
			Resources.UnloadUnusedAssets();
			System.GC.Collect();
			
			// and reset the other stuff
			brickID = Path.GetFileNameWithoutExtension(inputFiles[currentFile]);
			brickHasUVs = false;
			
			// LOAD MESHES FOR THIS FRAME'S BRICK
			
			//Debug.Log("Loading " + brickID + ", brick " + (currentFile + 1) + " of " + inputFiles.Length);
			for (int i = 0; i < 100; i++)
			{
				if (i == 0)
				{
					meshes.Add(LoadMesh(inputFiles[currentFile]));
				}
				else
				{
					if (File.Exists(inputFiles[currentFile] + i))
					{
						meshes.Add(LoadMesh(inputFiles[currentFile] + i));
					}
					else
					{
						break;
					}
				}
			}
			
			// SHOW THE MESHES IN THE SCENE, EXPORT, DONE
			
			foreach (Mesh mesh in meshes)
			{
				meshGameObjects.Add(PlopMeshIntoScene(mesh));
			}
			Export();
			currentFile++;
			timeSpan = (DateTime.Now - startTime);
			if (currentFile == inputFiles.Length)
			{
				QualitySettings.vSyncCount = 1; // re-enable vsync
				state = State.Done;
			}
		}
	}
	
	GameObject PlopMeshIntoScene(Mesh mesh)
	{
		GameObject newGameObject = new GameObject();
		newGameObject.transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
		MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();
		meshRenderer.material = brickMaterial;
		meshFilter.mesh = mesh;
		return newGameObject;
	}
	
	Mesh LoadMesh(string filePath)
	{
		FileStream fileStream = new FileStream(filePath, FileMode.Open);
		BinaryReader binaryReader = new BinaryReader(fileStream);
		
		binaryReader.BaseStream.Position = 4; // skip header
		UInt32 vertexCount = binaryReader.ReadUInt32();
		UInt32 indexCount = binaryReader.ReadUInt32();
		Flags flags = (Flags)binaryReader.ReadUInt32();
		//Debug.Log(Path.GetFileName(filePath) + " flags: " + flags);
		
		// vertices
		Vector3[] vertices = new Vector3[vertexCount];
		for (int i = 0; i < vertexCount; i++)
		{
			vertices[i] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		
		// normals
		Vector3[] normals = new Vector3[vertexCount];
		if ((flags & Flags.Normals) == Flags.Normals)
		{
			for (int i = 0; i < vertexCount; i++)
			{
				normals[i] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
			}
		}
		// dummy normals, not aware of any g files that lack normals but it's theoretically possible
		else
		{
			Debug.LogWarning(Path.GetFileName(filePath) + " has no normals");
			for (int i = 0; i < vertexCount; i++)
			{
				normals[i] = Vector3.up;
			}
		}
		
		// uv
		Vector2[] uv = new Vector2[vertexCount];
		if ((flags & Flags.UV) == Flags.UV)
		{
			brickHasUVs = true;
			for (int i = 0; i < vertexCount; i++)
			{
				uv[i] = new Vector2(binaryReader.ReadSingle(), -binaryReader.ReadSingle() + 1.0f);
			}
		}
		// set UVs to zero if mesh lacks them (will only be used if other meshes for the brick *do* use UVs)
		else
		{
			for (int i = 0; i < vertexCount; i++)
			{
				uv[i] = Vector2.zero;
			}
		}
		
		// triangles
		int[] triangles = new int[indexCount];
		for (int i = 0; i < indexCount; i++)
		{
			triangles [i] = (int)binaryReader.ReadUInt32();
		}
		
		// done reading
		binaryReader.Close();
		// according to some old stackoverflow post just closing the reader should (might) be enough, but just in case
		fileStream.Close();
		
		// return mesh
		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // for any super huge meshes with more than 65535 verts (baseplates and such)
		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uv;
		mesh.triangles = triangles;
		return mesh;
	}
	
	/*
		obj related code modified from the 3DXML project
		which was modified from this:
		http://wiki.unity3d.com/index.php?title=ExportOBJ
		which was modified from THIS:
		http://wiki.unity3d.com/index.php?title=ObjExporter
		oh yeah and the above g loading code is revised from something Simon (LU fan server dev guy) threw together once upon a time
		also used some documentation on the format from lcdr (or whoever else contributed to those docs) to grab the UV coords
	*/
	
	int startIndex = 0;
	
	void Export()
	{
		startIndex = 0;
		StringBuilder meshString = new StringBuilder();
		switch (conversionMode)
		{
			case ConversionModes.Default:
				for (int i = 0; i < meshes.Count; i++)
				{
					meshString.Append("\ng ").Append("g" + i).Append("\n");
					meshString.Append(MeshToString(meshes[i], brickHasUVs));
				}
				break;
			case ConversionModes.NoUVs:
				for (int i = 0; i < meshes.Count; i++)
				{
					meshString.Append("\ng ").Append("g" + i).Append("\n");
					meshString.Append(MeshToString(meshes[i], false));
				}
				break;
			case ConversionModes.NoUVsAndNoGroups:
				meshString.Append(AllMeshesToString());
				break;
		}
		File.WriteAllText(outputDirectory + Path.DirectorySeparatorChar + brickID + ".obj", meshString.ToString());
		//Debug.Log("Saved file " + brickID + ".obj");
	}
	
	string MeshToString(Mesh m, bool includeUVs)
	{
		StringBuilder sb = new StringBuilder();
		
		foreach(Vector3 v in m.vertices)
		{
			sb.Append(string.Format("v {0} {1} {2}\n",v.x,v.y,v.z));
		}
		sb.Append("\n");
		foreach(Vector3 v in m.normals)
		{
			sb.Append(string.Format("vn {0} {1} {2}\n",v.x,v.y,v.z));
		}
		sb.Append("\n");
		if (includeUVs)
		{
			foreach(Vector3 v in m.uv)
			{
				sb.Append(string.Format("vt {0} {1}\n",v.x,v.y));
			}
			sb.Append("\n");
		}
		if (includeUVs)
		{
			for (int i=0;i<m.triangles.Length;i+=3)
			{
				sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", 
					m.triangles[i]+1+startIndex, m.triangles[i+1]+1+startIndex, m.triangles[i+2]+1+startIndex));
			}
		}
		else
		{
			for (int i=0;i<m.triangles.Length;i+=3)
			{
				sb.Append(string.Format("f {0}//{0} {1}//{1} {2}//{2}\n", 
					m.triangles[i]+1+startIndex, m.triangles[i+1]+1+startIndex, m.triangles[i+2]+1+startIndex));
			}
		}
		
		startIndex += m.vertices.Length;
		return sb.ToString();
	}
	
	string AllMeshesToString()
	{
		StringBuilder sb = new StringBuilder();
		
		foreach (Mesh m in meshes)
		{
			foreach(Vector3 v in m.vertices)
			{
				sb.Append(string.Format("v {0} {1} {2}\n",v.x,v.y,v.z));
			}
		}
		sb.Append("\n");
		foreach (Mesh m in meshes)
		{
			foreach(Vector3 v in m.normals)
			{
				sb.Append(string.Format("vn {0} {1} {2}\n",v.x,v.y,v.z));
			}
		}
		sb.Append("\n");
		foreach (Mesh m in meshes)
		{
			for (int i=0;i<m.triangles.Length;i+=3)
			{
				sb.Append(string.Format("f {0}//{0} {1}//{1} {2}//{2}\n", 
					m.triangles[i]+1+startIndex, m.triangles[i+1]+1+startIndex, m.triangles[i+2]+1+startIndex));
			}
			startIndex += m.vertices.Length;
		}
		
		return sb.ToString();
	}
	
	// not using but could be useful if we do duplicate vertex merging at some point (existing code for that from the 3DXML project doesn't like it)
	/*
	Mesh CombineMeshes()
	{
		int blahStartIndex = 0;
		
		List<Vector3> vertices = new List<Vector3>();
		List<Vector3> normals = new List<Vector3>();
		List<int> triangles = new List<int>();
		
		foreach (Mesh m in meshes)
		{
			foreach(Vector3 v in m.vertices)
			{
				vertices.Add(v);
			}
		}
		
		foreach (Mesh m in meshes)
		{
			foreach(Vector3 v in m.normals)
			{
				normals.Add(v);
			}
		}
		
		foreach (Mesh m in meshes)
		{
			for (int i = 0; i < m.triangles.Length; i++)
			{
				triangles.Add(m.triangles[i] + blahStartIndex);
			}
			blahStartIndex += m.vertices.Length;
		}
		
		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // for any super huge meshes with more than 65535 verts (baseplates and such)
		mesh.vertices = vertices.ToArray();
		mesh.normals = normals.ToArray();
		mesh.triangles = triangles.ToArray();
		return mesh;
	}
	*/
}

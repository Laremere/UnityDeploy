using UnityEditor;
using UnityEngine;

using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

public class UnityDeploy : EditorWindow {
	//Folder within unity project to put built executables
	private static string appFolderLocation = "UnityDeployApp";

	private bool serverRunning = false;
	private int port = 2667;
	private TcpListener tcpListener;
	private List<UnityDeployClient> clients;


	[MenuItem ("Window/Unity Deploy")]
	public static void Init() {
		//Get existing open window or if none, make a new one:
		UnityDeploy window = (UnityDeploy)EditorWindow.GetWindow(typeof(UnityDeploy));
		window.title = "Unity Deploy";
	}

	//Stop server when destroyed, not like it matters though
	public void OnDestroy(){
		if(serverRunning){
			stopServer();
		}
	}

	public void Update () {
		if (serverRunning){

			//If we expect the server to be running, but it isn't, restart it.
			//Happens on a build or whenever Unity serializes and recreates everything.
			if (tcpListener == null){
				startServer();
			}

			//Accepts clients
			while(tcpListener.Pending()){
				clients.Add(new UnityDeployClient(tcpListener.AcceptTcpClient()));
			}
		}
	}

	public void OnGUI(){

		GUILayout.Label("Server Settings:", EditorStyles.boldLabel);
		if(serverRunning){
			if (GUILayout.Button("Stop Server")){
				stopServer();
			}
			GUI.enabled = false; //Disable port number box while server is running
		} else {
			if (GUILayout.Button ("Start Server")){
				startServer();
			}
		}
		port = EditorGUILayout.IntField("Port Number:", port);
		GUI.enabled = true;

		GUILayout.Label("Commands:", EditorStyles.boldLabel);
		bool sendAndRestart = GUILayout.Button("Send and Restart");
		if (GUILayout.Button("Send") || sendAndRestart){
			foreach( UnityDeployClient client in clients){
				//Stop each client.
				client.send("stop");
				//Clean the working space.
				client.send("clearDirectory");
				//Instruct creation of each directory.
				string searchPath = appFolderLocation + "/win32/";
				foreach(string filepath in Directory.GetDirectories(searchPath, "*", SearchOption.AllDirectories)){
					client.send("directory " + relFilepathAsBase64(filepath, searchPath));
				}
				//Provide each file for transfer.
				foreach(string filepath in Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories)){
 					client.send("file " + relFilepathAsBase64(filepath, searchPath) + " " +
					            System.Convert.ToBase64String(File.ReadAllBytes(filepath)));
				}

				//Tell the client it's done.
				client.send("filesDone");
				if(sendAndRestart){
					//Start client if requested.
					client.send("start");
				}
			}
		}

		if (GUILayout.Button("Stop")){
			//Instruct each client to stop.
			foreach( UnityDeployClient client in clients){
				client.send("stop");
			}
		}

		if (GUILayout.Button("Start")){
			//Instruct each client to start.
			foreach( UnityDeployClient client in clients){
				client.send("start");
			}
		}

		if (GUILayout.Button("Restart")){
			//Instruct each client to stop and then start again
			foreach( UnityDeployClient client in clients){
				client.send("stop");
				client.send("start");
			}
		}

		//List the clients and their status.
		GUILayout.Label("Connected Clients:", EditorStyles.boldLabel);
		if(!serverRunning){
			GUILayout.Label("Start server to allow clients to connect.");
		} else {
			foreach( UnityDeployClient client in clients){
				GUILayout.Label(client.info());
			}
		}

		//Instructions to build.  At bottom because otherwise it throws errors. (yup....)
		GUILayout.Label("Build:", EditorStyles.boldLabel);
		buildButton("Win", BuildTarget.StandaloneWindows, "win32", ".exe");
		//buildButton("OSX", BuildTarget.StandaloneOSXIntel, "mac32", ".app");
		//buildButton("Linux", BuildTarget.StandaloneLinux, "linux32", "");

	}

	private void startServer(){
		//Start tcp listener and create a list for clients
		serverRunning = true;
		tcpListener = new TcpListener(IPAddress.Any, port);
		tcpListener.Start();
		clients = new List<UnityDeployClient>();
	}

	private void stopServer(){
		//Destroy each client and stop listening for new clients.
		serverRunning = false;
		foreach(UnityDeployClient client in clients){
			client.Stop();
		}
		tcpListener.Stop();
		tcpListener = null;
		clients = null;
	}

	//Finds the relative filepath, and returns it in base64 encoding
	private string relFilepathAsBase64(string filepath, string relative){
		string relFilepath = filepath.Substring(relative.Length).Replace("\\","/");
		byte[] bytes = System.Text.UTF8Encoding.UTF8.GetBytes(relFilepath);
		return System.Convert.ToBase64String(bytes);
	}

	//Creates a gui button which will build the project for a targeted platform
	private void buildButton(string label, BuildTarget target, string folder, string extension){
		if(GUILayout.Button (label)){
			if(! System.IO.Directory.Exists(appFolderLocation + "/")){
				System.IO.Directory.CreateDirectory(appFolderLocation + "/");
			}
			if(! System.IO.Directory.Exists(appFolderLocation + "/" + folder + "/")){
				System.IO.Directory.CreateDirectory(appFolderLocation + "/" + folder + "/");
			}

			string[] levels = new string[0];

			string errorMessage = BuildPipeline.BuildPlayer(levels, appFolderLocation + "/" + folder + "/UnityDeployApplication" + extension, target, BuildOptions.None);
			if (errorMessage != ""){
				Debug.LogError(errorMessage);
			}
		}
	}
}

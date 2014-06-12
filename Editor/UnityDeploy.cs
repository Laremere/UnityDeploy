using UnityEditor;
using UnityEngine;

using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

public class UnityDeploy : EditorWindow {
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


	public void OnDestroy(){
		if(serverRunning){
			stopServer();
		}
	}

	public void Update () {
		if (serverRunning){
			if (tcpListener == null){
				startServer();
			}
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
			GUI.enabled = false;
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
				client.send("stop");
				client.send("clearDirectory");
				string searchPath = appFolderLocation + "/win32/";
				foreach(string filepath in Directory.GetDirectories(searchPath, "*", SearchOption.AllDirectories)){
					client.send("directory " + relFilepathAsBase64(filepath, searchPath));
				}
	
				foreach(string filepath in Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories)){
 					client.send("file " + relFilepathAsBase64(filepath, searchPath) + " " +
					            System.Convert.ToBase64String(File.ReadAllBytes(filepath)));
				}
				client.send("filesDone");
				if(sendAndRestart){
					client.send("start");
				}
			}
		}
		if (GUILayout.Button("Stop")){
			foreach( UnityDeployClient client in clients){
				client.send("stop");
			}
		}
		if (GUILayout.Button("Start")){
			foreach( UnityDeployClient client in clients){
				client.send("start");
			}
		}
		if (GUILayout.Button("Restart")){
			foreach( UnityDeployClient client in clients){
				client.send("stop");
				client.send("start");
			}
		}



		GUILayout.Label("Connected Clients:", EditorStyles.boldLabel);
		if(!serverRunning){
			GUILayout.Label("Start server to allow clients to connect.");
		} else {
			foreach( UnityDeployClient client in clients){
				GUILayout.Label(client.info());
			}
		}

		GUILayout.Label("Build:", EditorStyles.boldLabel);
		buildButton("Win", BuildTarget.StandaloneWindows, "win32", ".exe");
		buildButton("OSX", BuildTarget.StandaloneOSXIntel, "mac32", ".app");
		buildButton("Linux", BuildTarget.StandaloneLinux, "linux32", "");

	}

	private void startServer(){
		serverRunning = true;
		tcpListener = new TcpListener(IPAddress.Any, port);
		tcpListener.Start();
		clients = new List<UnityDeployClient>();
	}

	private void stopServer(){
		serverRunning = false;
		foreach(UnityDeployClient client in clients){
			client.Stop();
		}
		tcpListener.Stop();
		tcpListener = null;
		clients = null;
	}

	private string relFilepathAsBase64(string filepath, string relative){
		string relFilepath = filepath.Substring(relative.Length).Replace("\\","/");
		byte[] bytes = System.Text.UTF8Encoding.UTF8.GetBytes(relFilepath);
		return System.Convert.ToBase64String(bytes);
	}

	private void buildButton(string label, BuildTarget target, string folder, string extension){
		if(GUILayout.Button (label)){
			if(! System.IO.Directory.Exists(appFolderLocation + "/")){
				System.IO.Directory.CreateDirectory(appFolderLocation + "/");
			}
			if(! System.IO.Directory.Exists(appFolderLocation + "/" + folder + "/")){
				System.IO.Directory.CreateDirectory(appFolderLocation + "/" + folder + "/");
			}

			string errorMessage = BuildPipeline.BuildPlayer(null, appFolderLocation + "/" + folder + "/UnityDeployApplication" + extension, target, BuildOptions.None);
			if (errorMessage != ""){
				Debug.LogError(errorMessage);
			}
		}
	}
}

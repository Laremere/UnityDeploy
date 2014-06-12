using UnityEngine;

using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class UnityDeployClient{
	private TcpClient client;
	private Object infoLock = new Object();
	private string name = "unkown";
	private string state = "unkown";
	private Thread recievingThread;
	private Thread sendingThread;
	private Queue<string> sendQueue = new Queue<string>();

	private UnityDeployClient(){}

	public UnityDeployClient(TcpClient client){
		this.client = client;
		recievingThread = new Thread(new ThreadStart(handleRecieves));
		recievingThread.Start();
		sendingThread = new Thread(new ThreadStart(handleSends));
		sendingThread.Start();
	}

	private void handleSends(){
		StreamWriter writer = new StreamWriter(client.GetStream());
		writer.AutoFlush = true;
		lock(sendQueue){
			while (true){
				while(sendQueue.Count == 0){
					Debug.Log("Waiting for command");
					Monitor.Wait(sendQueue);
				}
				writer.Write(sendQueue.Dequeue() + "\n");
			}
		}
	}

	public void send(string command){
		lock(sendQueue){
			sendQueue.Enqueue(command);
			if(sendQueue.Count == 1){
				Monitor.PulseAll(sendQueue);
			}
		}
	}
	
	private void handleRecieves(){

		StreamReader reader = new StreamReader(client.GetStream());
		while(true){
			string[] command = reader.ReadLine().Split(new char[]{' '});
			if (command.Length == 0){
				continue;
			}
			switch (command[0]){
			case "name":
				lock(infoLock){
					name = command[1];
				}
				break;
			case "state":
				lock(infoLock){
					state = command[1];
				}
				break;
			default:
				Debug.Log("Unkown client command: " + command[0]);
				break;
			}
		}
	}

	public void Stop(){
		client.Close();
		recievingThread.Abort();
		sendingThread.Abort();
	}

	public string info(){
		lock(infoLock){
			return name + ": " + state;
		}
	}

	public string version(){
		return "win32";
	}

	public bool alive(){
		return true;
	}
}

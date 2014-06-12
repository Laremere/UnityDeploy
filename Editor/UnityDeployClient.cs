using UnityEngine;

using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class UnityDeployClient{
	private TcpClient client;

	//Lock for access of general client info
	private Object infoLock = new Object();
	private string name = "unkown";
	private string state = "unkown";

	//Threads for handling the recieving and sending of data
	private Thread recievingThread;
	private Thread sendingThread;

	//Queue of commands to send to client
	private Queue<string> sendQueue = new Queue<string>();

	private UnityDeployClient(){}

	public UnityDeployClient(TcpClient client){
		//Store tcp connection and start threads
		this.client = client;
		recievingThread = new Thread(new ThreadStart(handleRecieves));
		recievingThread.Start();
		sendingThread = new Thread(new ThreadStart(handleSends));
		sendingThread.Start();
	}

	private void handleSends(){
		//Send string command to client in a loop
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
		//Adds each new string to the end of the queue
		lock(sendQueue){
			sendQueue.Enqueue(command);
			if(sendQueue.Count == 1){
				Monitor.PulseAll(sendQueue);
			}
		}
	}
	
	private void handleRecieves(){
		//Get each new command and do relevant action.
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
		//Should cause alive() to return false, oops.
	}

	public string info(){
		lock(infoLock){
			return name + ": " + state;
		}
	}

	public string version(){
		//Should return which folder to use to handle different
		//types of clients, oops
		return "win32";
	}

	public bool alive(){
		//Should be used to remove dead clients, oops.
		return true;
	}
}

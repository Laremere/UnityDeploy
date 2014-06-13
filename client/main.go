package main

import (
	"bufio"
	"encoding/base64"
	"flag"
	"fmt"
	"log"
	"net"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"time"
)

//Directory next to client executable to use for
//the application
var applicationDirectory = "appDir"

func main() {
	client := Client{running: true, clientState: "Waiting_for_commands"}

	//Parse input parameters
	flag.StringVar(&client.serverAddress, "address", "localhost", "address of Unity server")
	flag.Int64Var(&client.serverPort, "port", 2667, "port of Unity server")
	flag.StringVar(&client.clientName, "name", "unnamed", "identifying name")
	flag.Parse()

	//Input parameter checks
	if client.serverPort <= 0 || client.serverPort > 65535 {
		log.Fatalf("Invalid port number %d", client.serverPort)
	}
	if strings.Contains(client.clientName, "\n") {
		log.Fatal("client name must not contain a semicolon")
	}
	if strings.Contains(client.clientName, " ") {
		log.Fatal("client name must not contain a space")
	}

	var err error
	//error string avoids repeated error messages in the log
	var errStr string
	backOff := time.Second / 4

	for client.running {
		//Attempt to connect to server
		client.conn, err = net.Dial("tcp", client.serverAddress+":"+strconv.FormatInt(client.serverPort, 10))
		if err != nil {
			//Log error if unique
			if err.Error() != errStr {
				errStr = err.Error()
				log.Println(errStr)
			}

			//Notify of backoff times, but limit to 16 seconds,
			//and don't spam the log after that.
			if backOff < time.Second*16 {
				backOff *= 2
				if backOff < time.Second*16 {
					log.Printf("Failed to connect.  Retrying in %s.\r\n", backOff)
				} else {
					log.Printf("Failed to connect.  Retrying every 16s.\r\n")
				}
			}
			time.Sleep(backOff)
			continue
		}

		//Reset backoff info to defaults
		backOff = time.Second / 4
		errStr = ""

		//Handle the connection here
		log.Println("Connected")
		err = client.handle()

		//If we encountered an error, we should try reconnecting
		if err != nil {
			log.Println(err)
			log.Println("Reconnecting...")
		} else {
			break
		}
	}
}

//Main struct holding the state of the connection
type Client struct {
	running       bool
	serverAddress string
	serverPort    int64
	clientName    string
	clientState   string
	conn          net.Conn
	process       *exec.Cmd
}

//Main recieving loop to handle server's instructions
func (client *Client) handle() error {
	handlers := map[string]func([]string) error{
		"clearDirectory": client.clearDirectory,
		"directory":      client.directory,
		"file":           client.file,
		"start":          client.start,
		"stop":           client.stop,
		"filesDone":      client.filesDone,
	}

	//Always send client info first
	fmt.Fprintf(client.conn, "state %s\n", client.clientState)
	fmt.Fprintf(client.conn, "name %s\n", client.clientName)
	fmt.Fprintf(client.conn, "OS %s %s\n", runtime.GOOS, runtime.GOARCH)

	reader := bufio.NewReader(client.conn)
	for {
		//Read next command and arguements
		commandStr, err := reader.ReadString(byte('\n'))
		if err != nil {
			return err
		}
		command := strings.Split(commandStr[:len(commandStr)-1], " ")

		//Use the handler functions to dispatch to respective function
		commandFunc, ok := handlers[command[0]]
		if ok {
			err = commandFunc(command)
			if err != nil {
				log.Println("Error in command", command[0])
				panic(err)
			}
		} else {
			log.Println("Unkown command", command[0])
		}
	}
}

//Update the server on the status of the client
func (client *Client) state(newState string) error {
	client.clientState = newState
	_, err := fmt.Fprintf(client.conn, "state %s\n", client.clientState)
	return err
}

//Cleans the directory for the application so a new version can be put there
//Retries once a second until it succeeds
func (client *Client) clearDirectory([]string) error {
	err := client.state("Clearing_directory")
	if err != nil {
		return err
	}

	for true {
		err = os.RemoveAll(applicationDirectory)
		if err != nil {
			log.Println("Trouble clearing directory  (retrying shortly):", err)
			err := client.state("Trouble_clearing_directory")
			if err != nil {
				return err
			}
			time.Sleep(time.Second)
		} else {
			break
		}
	}
	err = os.Mkdir(applicationDirectory, os.ModeDir)
	return err
}

//Instructs the client of a folder it should make
func (client *Client) directory(command []string) error {
	err := client.state("Loading_folders")
	if err != nil {
		return err
	}

	pathBytes, err := base64.StdEncoding.DecodeString(command[1])
	if err != nil {
		return err
	}
	err = os.MkdirAll(filepath.Join(applicationDirectory, string(pathBytes)), os.ModeDir)
	return err
}

//Instructs the client of a file it should create
//along with the contents of the file
func (client *Client) file(command []string) error {
	err := client.state("Loading_files")
	if err != nil {
		return err
	}

	pathBytes, err := base64.StdEncoding.DecodeString(command[1])
	if err != nil {
		return err
	}
	file, err := os.Create(filepath.Join(applicationDirectory, string(pathBytes)))
	if err != nil {
		return err
	}
	defer file.Close()
	fileBytes, err := base64.StdEncoding.DecodeString(command[2])
	if err != nil {
		return err
	}
	_, err = file.Write(fileBytes)
	return err
}

//Starts the application
func (client *Client) start([]string) error {
	if client.process != nil {
		return nil
	}

	err := client.state("Running")
	if err != nil {
		return err
	}

	fileLocation, err := filepath.Abs(filepath.Join(applicationDirectory, client.applicationName()))
	if err != nil {
		return err
	}
	client.process = exec.Command(fileLocation)
	client.process.Dir = applicationDirectory
	err = client.process.Start()
	return err
}

//State to instruct the server dialogue that the files are all loaded
func (client *Client) filesDone([]string) error {
	return client.state("Waiting")
}

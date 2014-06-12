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

var applicationDirectory = "appDir"

func main() {
	client := Client{running: true, clientState: "Waiting_for_commands"}
	flag.StringVar(&client.serverAddress, "ip", "localhost", "ip address of Unity server")
	flag.Int64Var(&client.serverPort, "port", 2667, "port of Unity server")
	flag.StringVar(&client.clientName, "name", "unnamed", "identifying name")
	flag.Parse()

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
	var errStr string
	backOff := time.Second / 4

	for client.running {
		client.conn, err = net.Dial("tcp", client.serverAddress+":"+strconv.FormatInt(client.serverPort, 10))
		if err != nil {
			if err.Error() != errStr {
				errStr = err.Error()
				log.Println(errStr)
			}
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

		backOff = time.Second / 4 //Reset to defaults
		errStr = ""

		log.Println("Connected")
		err = client.handle()
		if err != nil {
			log.Println(err)
			log.Println("Reconnecting...")
		} else {
			break
		}
	}
}

type Client struct {
	running       bool
	serverAddress string
	serverPort    int64
	clientName    string
	clientState   string
	conn          net.Conn
	process       *exec.Cmd
}

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
		commandStr, err := reader.ReadString(byte('\n'))
		if err != nil {
			return err
		}
		command := strings.Split(commandStr[:len(commandStr)-1], " ")
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

func (client *Client) state(newState string) error {
	client.clientState = newState
	_, err := fmt.Fprintf(client.conn, "state %s\n", client.clientState)
	return err
}

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
		}
		break
	}
	err = os.Mkdir(applicationDirectory, os.ModeDir)
	return err
}

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

func (client *Client) start([]string) error {
	err := client.state("Running")
	if err != nil {
		return err
	}

	if client.process != nil {
		return nil
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

func (client *Client) filesDone([]string) error {
	return client.state("Waiting")
}

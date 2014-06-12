package main

import (
	"log"
	"os/exec"
)

//Forces the application to close, platform dependant since
//Unity creates a new process which can't be killed from the
//started process.
func (client *Client) stop([]string) error {
	if client.process == nil {
		return nil
	}

	err := client.state("Stopped")
	if err != nil {
		return err
	}

	//On windows, use task kill with a force
	process := exec.Command("cmd", "/C", "taskkill", "/F", "/IM", client.applicationName())
	err = process.Start()
	if err != nil {
		return err
	}
	err = process.Wait()
	if err != nil {
		switch err := err.(type) {
		default:
			return err
		case *exec.ExitError:
			log.Println("Error terminating program, this is usually when the program exited on its own:", err)
		}
	}
	client.process = nil
	return nil
}

//Return the platform dependant name of the application
func (client *Client) applicationName() string {
	return "UnityDeployApplication.exe"
}

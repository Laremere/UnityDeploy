package main

import (
	"log"
	"os/exec"
)

func (client *Client) stop([]string) error {
	err := client.state("Stopped")
	if err != nil {
		return err
	}

	if client.process == nil {
		return nil
	}

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

func (client *Client) applicationName() string {
	return "UnityDeployApplication.exe"
}

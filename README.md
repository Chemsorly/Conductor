Conductor is a application based on .NET core designed for distributed machine learning. It features a client-server architecture for horizontal scaling in a containerized environment.
The server application defines work packages and assigns them to the clients. The clients fetch work from the server, do the heavy lifting and deliver the newly generated files (i.e. machine learning models) back to the server. The main purpose is to provide a way to horizontally scale the machine learning prozess as easy as possible. Simply add more containers.

# Build status
| branch | pipeline | docker:server | docker:client(CPU) | docker:client(GPU) |
| ------ | -------- | ------------- | ------------------ | ------------------ |
| master | ![pipeline](https://git.chemsorly.com/Chemsorly/conductor/badges/master/build.svg) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-server-master.svg)](https://microbadger.com/images/chemsorly/conductor:latest-server-master) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-cpu-master.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-cpu-master) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-gpu-master.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-gpu-master) | 
| nightly | ![pipeline](https://git.chemsorly.com/Chemsorly/conductor/badges/nightly/build.svg) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-server-nightly.svg)](https://microbadger.com/images/chemsorly/conductor:latest-server-nightly) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-cpu-nightly.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-cpu-nightly) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-gpu-nightly.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-gpu-nightly) |

# Server
The server is a .NET core 2.1 application with a SignalR hub on port 8080 (via kestrel/iis) with endpoint /signalr for the clients.
## Working directory
It creates a folder at %AppData% (Windows) or /home (Linux) as working directory.
The working directory contains two folders:
* assets: contains all working packages and the results. A working package is a directory that contains files and subdirectories. The entire directory gets send to the client as payload (except "models" and "evaluation" directories").
* config: contains the config.xml configuration file

The server is designed to connect to a SignalR frontend (see CONDUCTOR_HOST in docker-compose files), which can be used to remotely control the server and request predictions. However, it is not required and the server can be managed entirely via the xml files.

## Configuration file
The configuration file contains a list of versions (aka work packages) and the following parameters:
* ReserveNodes: a relative portion of nodes (as in percentage) to reserve for predictions. The reserve nodes do not accept training jobs. Value ranges from 0 to 1.

# Client
The client is a .NET core 2.1 application that connects to the server via SignalR. It periodically fetches work packages from the server and executes them. It is designed to scale horizontally in a containerized environment.

# Usage
## Define a version (aka work package)
A version (Conductor_Shared.Version) is a work package, than contains the definition for the payload that the client executes. Parameters need to be defined as follows:
* TargetModels: amount of target models to train. The server creates work packages equal to target models minus already trained models.
* TrainingCommands: a list of training commands (Conductor_Shared.Command), that are executed to train the model. A command contains the executable and arguments.
  * example: <FileName>/bin/bash</FileName> <Arguments>-c "python3 my_python_script.py my python script arguments"</Arguments>
* (optional) PredictionCommands: a list of prediction commands, that are executed to create ensemble predictions. Used for production use cases.
* (optional) DatasetType: Used to define evaluation metrics to use.
* (optional) Name: the name of the folder. Will be overriden if local type is used.

A version can be either defined global (via %AppDataPath%/config/config.xml) or via a version.xml in the work packages folder. The application reads in xml files.
## Docker
Prebuilt docker images are available at [DockerHub](https://hub.docker.com/r/chemsorly/conductor/). See .gitlab-ci.yml for instructions on how to build them yourself.
The client containers come in two flavors: CPU and GPU. The GPU containers require a working setup for nvidia-docker2.
It's recommended to use a container orchestration tool (e.g. [Rancher](https://rancher.com/)) to rapidly deploy many containers.

Example usage:
### Example usage server 
Windows: "docker-compose -f .\\Conductor_Server\docker-compose.master.yml up -d"

### Example usage client
Linux: "docker run -it -d -e CONDUCTOR_HOST=example.org:8080 chemsorly/conductor:latest-client-gpu-master"

## Without docker
### Server 
Run the Conductor_Server.dll with the dotnet command (requires .NET core runtime 2.1)
### Client 
Run the Conductor_Client.dll with the dotnet command and the server URL as first argument or via environment variable "CONDUCTOR_HOST" (requires .NET core runtime 2.1)

## Logs
The applications generate several logs. 
### Server
The server logs everything in the following format: [timestamp] [connected clients, queued work, active work]. The log is also saved in config/log.txt
### Client
The client forwards all console messages generated by the called application. On successful training, the log is written to the output directory.

# Development
(1) Install .NET core SDK 2.1.502  
(2) Open Solution in Visual Studio  
(3) Run the Server or Client project

# Credits
This application was initially developed during my master thesis and extended during the TransformingTransports research project, which received funding from the EU’s Horizon 2020 R&I programme under grant 731932.
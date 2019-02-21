Conductor is an application based on .NET core designed for distributed machine learning. It features a client-server architecture for horizontal scaling in a containerized environment.
The server application defines units of work and assigns them to the clients. The clients fetch work units from the server, do the heavy lifting and deliver the newly generated files (i.e. machine learning models) back to the server. 
The main purpose is to provide a way to horizontally scale the machine learning process as easy as possible. To do so, simply add more containers.

# Build status
| branch | pipeline | docker:server | docker:client(CPU) | docker:client(GPU) |
| ------ | -------- | ------------- | ------------------ | ------------------ |
| master | ![pipeline](https://git.chemsorly.com/Chemsorly/conductor/badges/master/build.svg) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-server-master.svg)](https://microbadger.com/images/chemsorly/conductor:latest-server-master) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-cpu-master.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-cpu-master) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-gpu-master.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-gpu-master) | 
| nightly | ![pipeline](https://git.chemsorly.com/Chemsorly/conductor/badges/nightly/build.svg) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-server-nightly.svg)](https://microbadger.com/images/chemsorly/conductor:latest-server-nightly) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-cpu-nightly.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-cpu-nightly) | [![](https://images.microbadger.com/badges/image/chemsorly/conductor:latest-client-gpu-nightly.svg)](https://microbadger.com/images/chemsorly/conductor:latest-client-gpu-nightly) |

# Server
The server is a .NET core 2.2 application with a SignalR hub on port 8080 (via kestrel/iis (asp.net core)) on endpoint /signalr for the clients.
## Working directory
It creates a folder at %AppData% (Windows) or /home (Linux) as working directory.
The working directory contains two folders:
* assets: contains all work collections and the results. A work collection is a directory that contains files and subdirectories. The server sends it to the client as payload (except "models" and "evaluation" directories, which are reserved for results).
* config: contains the config.xml configuration file and a logfile.

The server is designed to connect to a SignalR frontend (see CONDUCTOR_HOST in docker-compose files), which can be used to remotely control the server and request predictions. However, it is not required and the server can be managed entirely via the xml files.

## Configuration file
The configuration file contains a list of versions (see below for details) and the following parameters:
* ReserveNodes: a relative portion of nodes (as in percentage) to reserve for predictions. The reserve nodes do not accept training jobs. Value ranges from 0 to 1.

# Client
The client is a .NET core 2.2 application that connects to the server via SignalR. It periodically fetches work units from the server and executes it. It is designed to scale horizontally in a containerized environment.

# Usage
## Define a version (aka work collection)
A version (Conductor_Shared.Version) is a work collection, that contains the definition for the payload the client executes. Each work collection gets executed until a target amount of result artifacts is reached (i.e. trained models). Parameters need to be defined as follows:
* TargetModels: amount of target models to train. The server creates work units equal to target models minus already trained models.
* TrainingCommands: a list of training commands (Conductor_Shared.Command), that are executed to train the model. A command contains the executable and arguments.
  * example: (FileName)/bin/bash(/FileName) (Arguments)-c "python3 my_python_script.py my python script arguments"(/Arguments)
* (optional) PredictionCommands: a list of prediction commands, that are executed to create ensemble predictions. Used for production use cases.
* (optional) DatasetType: Used to define evaluation metrics to use.
* (optional) Name: the name of the folder. Will be overriden if local type is used.

A version can be either defined global (via %AppDataPath%/config/config.xml) or via a version.xml in the work collection folder. The application reads in xml files.
## Docker
Prebuilt docker images are available at [DockerHub](https://hub.docker.com/r/chemsorly/conductor/). See .gitlab-ci.yml for instructions on how to build them yourself.
The client containers come in two flavors: CPU and GPU. The GPU containers require a working setup for [nvidia-docker2](https://github.com/NVIDIA/nvidia-docker).
It's recommended to use a container orchestration tool (e.g. [Rancher](https://rancher.com/)) to rapidly deploy many containers.

## Logs
### Server
The server logs everything in the following format: [timestamp] [connected clients, queued work, active work]. The log is also saved in config/log.txt
### Client
The client forwards all console messages generated by the called application. On successful training, the log is written to the output directory.

# Development
(1) Install .NET core SDK 2.2.104 or newer  
(2) Open Solution in Visual Studio  
(3) Run the Server or Client project

# Getting started
You need to run a server and any amount of clients that connect to the server.
## Run the server
Can be run using pre-built docker images (windows only) or you can compile the application yourself.
### With docker-compose (recommended)
(On windows) 
```
docker-compose -f .\\Conductor_Server\docker-compose.master.yml up -d
```

### Without docker
Compile and run the Conductor_Server.dll with the dotnet command (requires .NET core runtime 2.1) and the frontend url as first argument. Can be left empty to not connect to a frontendM.ake sure the port (8080) is open in your firewall.
```
dotnet Conductor_Server.dll FRONTEND_HOSTNAME:PORT
```
## Run the client
The pre-built images for the client are only available for linux. 
### With docker (recommended)
```
docker run -it -d -e CONDUCTOR_HOST=example.org:8080 chemsorly/conductor:latest-client-gpu-master
```
### Wihtout docker
Compile and run the Conductor_Client.dll with the dotnet command and the server URL as first argument or via environment variable "CONDUCTOR_HOST" (requires .NET core runtime 2.1)
```
dotnet Conductor_Server.dll SERVER_HOSTNAME:PORT
```
### Example configuration (version.xml)
```
<?xml version="1.0"?>
<Version xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Created>2018-12-06T14:22:16.782621+01:00</Created>
  <LastUpdated>2018-12-06T14:22:16.7826263+01:00</LastUpdated>
  <Name>folder name</Name>
  <DatasetType>Cargo2000</DatasetType>
  <VersionStatus />
  <TrainingCommands>
    <VersionCommand>
      <FileName>/bin/bash</FileName>
      <Arguments>-c "python3 c2k_train_and_predict.py param1 param2 paramn"</Arguments>
    </VersionCommand>
  </TrainingCommands>
  <PredictionCommands />
  <TargetModels>100</TargetModels>
</Version>
```


# Credits
This application was initially developed during my master thesis and extended during the TransformingTransports research project, which received funding from the EU’s Horizon 2020 R&I programme under grant 731932.
SHELL := /bin/bash

.PHONY: build test quality

build:
	@export DOTNET_ROOT="$$HOME/.dotnet"; export PATH="$$DOTNET_ROOT:$$PATH"; dotnet build ClampDown.sln -c Debug

test:
	@export DOTNET_ROOT="$$HOME/.dotnet"; export PATH="$$DOTNET_ROOT:$$PATH"; dotnet test ClampDown.sln -c Debug

quality:
	@export DOTNET_ROOT="$$HOME/.dotnet"; export PATH="$$DOTNET_ROOT:$$PATH"; dotnet format ClampDown.sln --verify-no-changes

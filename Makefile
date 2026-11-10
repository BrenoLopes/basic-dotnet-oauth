.PHONY: help up infra-up infra-down infra-restart infra-status infra-logs api dev build test migration-add migration-script migration-update migration-list migration-remove

COMPOSE ?= docker compose
DOTNET ?= dotnet
PROJECT ?= src/DotNetAuth
LAUNCH_PROFILE ?= http
DOTNET_EF ?= .tools/dotnet-ef/dotnet-ef.exe
MIGRATIONS_PROJECT ?= src/DotNetAuth.Infrastructure
STARTUP_PROJECT ?= src/DotNetAuth
DB_CONTEXT ?= DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext
MIGRATIONS_DIR ?= IdentityAccess/Persistence/Migrations

EF_ARGS = --project $(MIGRATIONS_PROJECT) --startup-project $(STARTUP_PROJECT) --context $(DB_CONTEXT)

help:
	@echo "make up              - sobe os containers de infraestrutura"
	@echo "make api             - inicia a API ASP.NET Core no Windows"
	@echo "make dev             - sobe a infraestrutura e inicia a API"
	@echo "make infra-status    - mostra o status dos containers"
	@echo "make infra-logs      - acompanha os logs dos containers"
	@echo "make infra-down      - para e remove os containers"
	@echo "make infra-restart   - reinicia os containers"
	@echo "make build           - compila a solução .NET"
	@echo "make test            - executa os testes"
	@echo "make migration-add NAME=AddClient - cria uma migration"
	@echo "make migration-script              - gera o script SQL das migrations"
	@echo "make migration-update              - aplica as migrations pendentes"
	@echo "make migration-list                - lista as migrations"
	@echo "make migration-remove              - remove a última migration"

up: infra-up

infra-up:
	$(COMPOSE) up -d

infra-down:
	$(COMPOSE) down

infra-restart: infra-down infra-up

infra-status:
	$(COMPOSE) ps

infra-logs:
	$(COMPOSE) logs -f

api:
	$(DOTNET) run --project $(PROJECT) --launch-profile $(LAUNCH_PROFILE)

dev:
	$(MAKE) infra-up
	$(DOTNET) run --project $(PROJECT) --launch-profile $(LAUNCH_PROFILE)

build:
	$(DOTNET) build DotNetAuth.slnx

test:
	$(DOTNET) test DotNetAuth.slnx

migration-add:
	$(if $(NAME),,$(error NAME is required. Example: make migration-add NAME=AddClient))
	$(DOTNET_EF) migrations add $(NAME) $(EF_ARGS) --output-dir $(MIGRATIONS_DIR)

migration-script:
	$(DOTNET_EF) migrations script $(EF_ARGS)

migration-update:
	$(DOTNET_EF) database update $(EF_ARGS)

migration-list:
	$(DOTNET_EF) migrations list $(EF_ARGS)

migration-remove:
	$(DOTNET_EF) migrations remove $(EF_ARGS)

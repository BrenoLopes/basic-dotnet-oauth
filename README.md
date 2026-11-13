# DotNetAuth

API REST criada com ASP.NET Core e .NET 10, com servidor OAuth 2.0/OpenID Connect baseado em OpenIddict.

## O que está incluído

- ASP.NET Core Identity com usuários, confirmação de e-mail, login por cookie e logout.
- Authorization Code + PKCE e Refresh Token em `/connect/authorize` e `/connect/token`.
- Discovery document em `/.well-known/openid-configuration`.
- Validação local dos access tokens para rotas protegidas.
- Rota autenticada de exemplo: `GET /api/profile`.
- MariaDB para Identity e OpenIddict, Valkey para cache e Mailpit para e-mails.
- RustFS para armazenamento S3-compatible e CloudBeaver para inspeção do MariaDB.

## Arquitetura DDD

O projeto é um monólito modular. A API continua no projeto `DotNetAuth`, enquanto o domínio, os casos de uso e os adaptadores técnicos são separados em projetos com responsabilidades distintas:

```text
DotNetAuth.Domain
        ↑
DotNetAuth.Application
        ↑
DotNetAuth.Infrastructure
        ↑
DotNetAuth
```

As setas apontam para a dependência: `Application` depende de `Domain`, `Infrastructure` depende de `Application` e `Domain`, e a API compõe todas as partes.

### Projetos

- `src/DotNetAuth.Domain`: modelo de domínio puro, sem referências a ASP.NET Core, EF Core, Identity, OpenIddict, Redis, Valkey ou SMTP.
- `src/DotNetAuth.Application`: casos de uso e contratos organizados em `IdentityAccess/UseCases` e `IdentityAccess/Contracts`.
- `src/DotNetAuth.Infrastructure`: implementações técnicas de Identity, OpenIddict, EF Core, MariaDB, Valkey, SMTP e migrações.
- `src/DotNetAuth`: API HTTP, DTOs, controllers, composição da aplicação e adaptadores específicos de HTTP.

Na API, os endpoints do contexto ficam agrupados em `Modules/IdentityAccess/Controllers`: `AuthController`, `AuthorizationController` e `ProfileController`. O `HealthController` permanece separado por ser uma preocupação transversal.

### Bounded context atual

O único bounded context existente é `IdentityAccess`.

- `User` é o aggregate root do domínio.
- `UserId` é o identificador de domínio.
- `EmailAddress` é um value object imutável.
- `ApplicationUser` é somente o modelo técnico do ASP.NET Identity.
- Credenciais, tokens, lockout e persistência continuam sob responsabilidade do Identity na Infrastructure.

O domínio protege as regras de criação e confirmação do usuário, mas não conhece banco de dados, cache, SMTP, APIs externas ou detalhes do protocolo OAuth.

### Como expandir um domínio

Ao adicionar uma capacidade de negócio, siga esta sequência:

1. Criar entidades, value objects, regras e agregados no projeto `DotNetAuth.Domain`.
2. Criar os casos de uso correspondentes no projeto `DotNetAuth.Application`.
3. Criar na `Application` os contratos necessários para recursos externos, como armazenamento, mensageria ou notificações.
4. Implementar esses contratos na `DotNetAuth.Infrastructure`.
5. Registrar as implementações em `AddInfrastructure`.
6. Criar DTOs e endpoints na API somente quando a capacidade precisar ser exposta por HTTP.
7. Adicionar testes unitários para o domínio e para os casos de uso, além de testes de integração para o fluxo completo.

Regras de organização:

- Entidades devem proteger invariantes por meio de métodos de negócio, em vez de expor setters públicos para qualquer estado.
- Value objects devem ser imutáveis e validar sua própria representação.
- O domínio não deve acessar banco, cache, SMTP ou APIs externas.
- Casos de uso devem coordenar as operações e contratos; regras de negócio pertencem ao domínio.
- Agregados devem ser pequenos e ter limites claros.
- Referências entre agregados devem usar identificadores, não referências diretas aos objetos internos.
- Não adicione CQRS, MediatR ou Event Sourcing sem uma necessidade real do domínio.

## Organização da infraestrutura

A Infrastructure está organizada primeiro pelo bounded context e depois pela capacidade técnica:

```text
src/DotNetAuth.Infrastructure
├── IdentityAccess
│   ├── Caching
│   ├── Email
│   ├── Identity
│   ├── OAuth
│   └── Persistence
│       └── Migrations
└── DependencyInjection
```

Responsabilidade de cada área:

- `IdentityAccess/Identity`: integração com `UserManager`, `SignInManager` e `ApplicationUser`, incluindo contas, autenticação e confirmação de e-mail.
- `IdentityAccess/OAuth`: seed e integração com OpenIddict.
- `IdentityAccess/Email`: envio SMTP para o Mailpit e opções de e-mail.
- `IdentityAccess/Caching`: implementação do cache de perfil usando Valkey/Redis.
- `IdentityAccess/Persistence`: EF Core, MariaDB, `ApplicationDbContext`, inicialização do banco e migrations.
- `DependencyInjection`: registro dos adaptadores, opções e configurações no método `AddInfrastructure`.
- `Modules/IdentityAccess/Composition` em `src/DotNetAuth`: adaptadores específicos da API HTTP, como a geração de links de confirmação.

Os controllers permanecem finos: recebem DTOs HTTP e chamam serviços da Application. Eles não devem depender diretamente de `UserManager`, `SignInManager`, `ApplicationDbContext`, `SmtpEmailSender` ou `IDistributedCache`.

### Como acrescentar uma infraestrutura

Para integrar uma tecnologia ou recurso externo novo:

1. Defina a interface na camada `Application`, se ela for necessária para um caso de uso.
2. Crie a implementação tecnológica na `Infrastructure` em uma pasta relacionada à capacidade.
3. Crie opções fortemente tipadas quando houver configuração.
4. Adicione o pacote NuGet somente ao projeto `DotNetAuth.Infrastructure`.
5. Registre a implementação em `AddInfrastructure`.
6. Adicione as variáveis correspondentes ao `.env.example` quando necessário.
7. Adicione ou altere o serviço no `compose.yaml` quando a infraestrutura precisar de um container local.
8. Crie testes unitários usando um fake para os casos de uso e testes de integração do adaptador quando isso for relevante.
9. Atualize o README e os comandos de inicialização.

A Application deve depender da abstração, e a API não deve conhecer a biblioteca ou o protocolo específico do adaptador.

### Como remover uma infraestrutura

1. Remova os usos da infraestrutura nos casos de uso e na composição da aplicação.
2. Remova a interface da Application se ela não for mais necessária.
3. Remova a implementação, as opções e o registro no DI.
4. Remova o pacote NuGet que ficou sem uso.
5. Remova a configuração correspondente de `appsettings`, `.env.example` e `compose.yaml`.
6. Remova volumes e serviços Docker somente quando for seguro descartar os dados locais.
7. Se a infraestrutura possuía tabelas, crie uma migration de remoção; não apague o modelo sem registrar a alteração no banco.
8. Use `rg` para procurar referências restantes e execute build e testes.

## Ambiente de desenvolvimento

O ASP.NET Core roda diretamente no Windows. O Docker Compose sobe somente a infraestrutura, publicada no `localhost` para a API:

| Serviço | Acesso local |
| --- | --- |
| API ASP.NET Core | `http://localhost:5004` |
| Mailpit | `http://localhost:8025` |
| RustFS S3 | `http://localhost:9000` |
| RustFS Console | `http://localhost:9001` |
| CloudBeaver | `http://localhost:8978` |
| MariaDB | `localhost:3306` |
| Valkey | `localhost:6379` |

Inicialize o ambiente com:

```powershell
Copy-Item .env.example .env
make dev
```

O Docker Compose carrega automaticamente o arquivo `.env` localizado na raiz do projeto ao resolver as variáveis usadas em `compose.yaml`. O arquivo `.env.example` é apenas um modelo e não é carregado automaticamente; por isso, copie-o para `.env` antes de subir os containers.

O alvo `make dev` executa `docker compose up -d` e inicia a API com `dotnet run`. Para executar cada parte separadamente, use `make infra-up` e `make api`.

O prefixo das chaves do cache Valkey/Redis é configurado em `Cache:InstanceName` nos arquivos `appsettings.json` e `appsettings.Development.json`. Com o valor `dotnetauth`, as chaves são armazenadas com o prefixo `dotnetauth:`.

O perfil `Development` usa as conexões locais e está configurado com `Database:ApplyMigrations=true`; por isso, a API aplica as migrations pendentes ao iniciar. Se a senha `MARIADB_PASSWORD` do `.env` for alterada, atualize também `ConnectionStrings__Default` antes de executar a API.

Para usar um SMTP externo, configure as opções da seção `Smtp` por configuração ou variáveis de ambiente. As credenciais não devem ser gravadas no repositório:

```text
Smtp__Host=smtp.example.com
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__Username=usuario-smtp
Smtp__Password=senha-smtp
Smtp__FromAddress=no-reply@example.com
```

As demais políticas operacionais também ficam no `appsettings`: `Database` controla migrations, versão do MariaDB, retries e inicialização; `Identity` controla senha, confirmação de e-mail e lockout; `Cache:ProfileExpirationMinutes` controla o TTL do perfil; e `OAuth` controla issuer, audience, scopes, PKCE, refresh token e o cliente de teste.

O CloudBeaver acessa o MariaDB pela rede interna do Compose usando:

- Host: `mariadb`
- Porta: `3306`
- Banco: `dotnetauth`
- Usuário: `dotnetauth`
- Senha: valor de `MARIADB_PASSWORD` no `.env`

Não use `docker compose down -v` em ambientes com dados importantes. Esse comando remove os volumes locais de MariaDB, Valkey, RustFS e CloudBeaver.

## Migrations e banco de dados

### Estrutura atual

O contexto do EF Core está em:

```text
DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext
```

As migrations ficam no projeto `DotNetAuth.Infrastructure`:

```text
src/DotNetAuth.Infrastructure/IdentityAccess/Persistence/Migrations
```

Os parâmetros dos comandos são sempre:

- Projeto de migrations: `src/DotNetAuth.Infrastructure`.
- Startup project: `src/DotNetAuth`.
- Contexto: `DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext`.
- Diretório de saída: `IdentityAccess/Persistence/Migrations`.

O startup project é necessário porque fornece a configuração e a composição da aplicação usadas para criar o `DbContext`.

### Instalar a ferramenta EF Core

A versão usada pelo projeto é `9.0.10`. No PowerShell, instale a ferramenta localmente no repositório:

```powershell
dotnet tool install --tool-path .tools/dotnet-ef dotnet-ef --version 9.0.10
```

Se a ferramenta já existir, atualize-a com:

```powershell
dotnet tool update --tool-path .tools/dotnet-ef dotnet-ef --version 9.0.10
```

Defina o executável para os comandos seguintes:

```powershell
$ef = ".tools/dotnet-ef/dotnet-ef.exe"
```

### Criar, revisar e aplicar uma migration

Para adicionar uma nova migration:

```powershell
& $ef migrations add AddNewTable `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext `
  --output-dir IdentityAccess/Persistence/Migrations
```

Para gerar o script SQL:

```powershell
& $ef migrations script `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext
```

Para aplicar todas as migrations pendentes:

```powershell
& $ef database update `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext
```

Para listar as migrations:

```powershell
& $ef migrations list `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext
```

Gerar uma migration somente cria arquivos no projeto. Ela não altera o banco. O banco só é alterado por `database update`, pela execução do script SQL em um ambiente controlado ou pelo fluxo de inicialização da API quando `Database:ApplyMigrations=true`.

Se a última migration ainda não foi aplicada e precisa ser corrigida, remova-a com:

```powershell
& $ef migrations remove `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext
```

Não remova uma migration que já foi aplicada em um ambiente compartilhado. Nesse caso, crie uma nova migration que corrija o estado anterior.

### Adicionar uma nova tabela

Para expandir o modelo persistido:

1. Crie ou altere o modelo persistido no projeto `DotNetAuth.Infrastructure`.
2. Configure a entidade, chaves, índices e relacionamentos no `ApplicationDbContext`.
3. Gere uma migration com `migrations add`.
4. Revise o código e o SQL gerado, especialmente operações destrutivas, índices e valores padrão.
5. Aplique a migration no ambiente de desenvolvimento com `database update` ou inicializando a API com `make dev`.
6. Execute `dotnet build` e `dotnet test`.

Se o modelo representa uma regra de negócio, mantenha a entidade e suas invariantes no `Domain` e use um modelo de persistência na `Infrastructure` quando os formatos forem diferentes. O `ApplicationDbContext` deve continuar sendo responsabilidade da Infrastructure.

### Reset do banco de desenvolvimento

Quando o ambiente de desenvolvimento puder ser recriado do zero:

```powershell
docker compose down -v
make dev
```

Isso remove os volumes locais de MariaDB, Valkey, RustFS e CloudBeaver. Nunca use esse reset para dados importantes, homologação ou produção.

## Páginas web de autenticação

O projeto possui páginas server-side Razor para registro e login. Elas usam o cookie do ASP.NET Identity, separado da autenticação Bearer usada pelas rotas de API.

Em `appsettings.json`, o recurso fica desabilitado por padrão:

```json
{
  "Web": {
    "Authentication": {
      "Registration": false
    }
  }
}
```

Em `Development`, ele fica habilitado por padrão em `appsettings.Development.json`:

```json
{
  "Web": {
    "Authentication": {
      "Registration": true
    }
  }
}
```

Também é possível configurar pela variável de ambiente:

```text
Web__Authentication__Registration=true
```

O login web fica sempre disponível. A flag controla apenas a página de registro:

- `GET /account/login`: formulário de login.
- `GET /account/register`: formulário de registro, quando `Registration` está habilitado.

Os endpoints JSON `/api/auth/register` e `/api/auth/login` continuam disponíveis independentemente da flag. O cookie web não autoriza `/api/profile`, que continua exigindo um access token Bearer.

Ao iniciar `/connect/authorize` sem sessão web, a aplicação sempre redireciona para `/account/login` com um `returnUrl` local. Depois do login, o fluxo OAuth retorna ao endpoint de autorização e preserva Authorization Code + PKCE.

## Fluxo de autenticação

1. `POST /api/auth/register` cria o usuário e envia o link de confirmação para o Mailpit.
2. Abra o link recebido em `http://localhost:8025`.
3. `POST /api/auth/login` cria a sessão de cookie usada pelo endpoint de autorização.
4. O cliente OAuth inicia Authorization Code + PKCE em `/connect/authorize`.
5. O código retornado pelo redirect URI é trocado por tokens em `/connect/token`.
6. Use `Authorization: Bearer <access_token>` ao chamar `/api/profile`.

O cliente público de desenvolvimento é criado automaticamente após a inicialização do banco:

- `client_id`: `dotnetauth-local`
- `redirect_uri`: `http://localhost:5004/oauth/callback`
- PKCE obrigatório

### Configurar Postman ou Insomnia

O fluxo abaixo usa Authorization Code + PKCE. O cliente local é público, portanto não existe `client_secret`.

Antes de começar:

```powershell
make dev
```

Confirme também que as páginas web estão habilitadas:

```text
Web__Authentication__Registration=true
```

#### 1. Criar e confirmar o usuário

Crie o usuário pelo Postman ou Insomnia com uma requisição:

```http
POST http://localhost:5004/api/auth/register
Content-Type: application/json
```

```json
{
  "email": "oauth-user@example.com",
  "password": "Abcdefgh",
  "displayName": "OAuth User"
}
```

A senha precisa ter pelo menos 8 caracteres, uma letra maiúscula e uma letra minúscula.

Depois:

1. Abra o Mailpit em `http://localhost:8025`.
2. Abra o e-mail de confirmação.
3. Acesse o link de confirmação.
4. Faça login no navegador em `http://localhost:5004/account/login`.

O login pelo navegador cria o cookie web usado pelo endpoint `/connect/authorize`.

#### 2. Valores OAuth

Use estes valores nos dois clientes:

| Campo | Valor |
| --- | --- |
| Grant Type | Authorization Code |
| Authorization URL | `http://localhost:5004/connect/authorize` |
| Access Token URL | `http://localhost:5004/connect/token` |
| Client ID | `dotnetauth-local` |
| Client Secret | deixar vazio |
| Redirect URI | `http://localhost:5004/oauth/callback` |
| Scope | `openid profile email api` |
| PKCE | habilitado |
| Code Challenge Method | `S256` |
| Client Authentication | enviar no body, quando essa opção existir |

O `redirect_uri` precisa ser exatamente igual ao URI cadastrado. O caminho `/oauth/callback` é usado como URI de retorno de desenvolvimento; ele não é uma página da aplicação.

#### 3. Postman

1. Crie uma requisição para `GET http://localhost:5004/api/profile`.
2. Abra a aba **Authorization**.
3. Em **Auth Type**, selecione **OAuth 2.0**.
4. Em **Configure New Token**, informe os valores da tabela anterior.
5. Se existir a opção, selecione **Authorization Code (With PKCE)**.
6. Use **Authorize using browser** para autenticar com a página web.
7. Clique em **Get New Access Token** e depois em **Use Token**.
8. Envie a requisição para `/api/profile`.

Dependendo da versão do Postman, o callback local pode terminar em uma página `404` depois do login. Isso é esperado: copie o parâmetro `code` da URL do navegador e faça a troca manual descrita na seção abaixo.

Veja também a [documentação oficial de OAuth 2.0 do Postman](https://learning.postman.com/docs/use/send-requests/authorization/oauth-20/).

#### 4. Insomnia

1. Crie uma requisição para `GET http://localhost:5004/api/profile`.
2. Abra a aba **Auth**.
3. Selecione **OAuth 2.0**.
4. Escolha **Authorization Code**.
5. Preencha Authorization URL, Access Token URL, Client ID, Redirect URL e Scope com os valores da tabela.
6. Habilite **PKCE** e selecione `SHA-256`.
7. Deixe o client secret vazio.
8. Inicie o fluxo de autorização pelo navegador.
9. Depois da troca do código, envie a requisição para `/api/profile`.

Se o Insomnia não capturar automaticamente o callback local, use a troca manual abaixo com o mesmo `code_verifier` gerado pelo cliente.

Veja também a [documentação oficial de autenticação de requisições do Insomnia](https://developer.konghq.com/insomnia/request-authentication/).

#### 5. Troca manual do código por token

Quando o navegador retornar para uma URL semelhante a esta:

```text
http://localhost:5004/oauth/callback?code=CODIGO_AQUI&iss=http%3A%2F%2Flocalhost%3A5004%2F
```

Copie somente o valor de `code`. Depois, no Postman ou Insomnia, crie:

```http
POST http://localhost:5004/connect/token
Content-Type: application/x-www-form-urlencoded
```

No corpo, escolha **x-www-form-urlencoded** e informe:

| Campo | Valor |
| --- | --- |
| `client_id` | `dotnetauth-local` |
| `grant_type` | `authorization_code` |
| `code` | código copiado do navegador |
| `redirect_uri` | `http://localhost:5004/oauth/callback` |
| `code_verifier` | mesmo valor usado para gerar o challenge |

A resposta conterá:

```json
{
  "token_type": "Bearer",
  "access_token": "...",
  "expires_in": 3600,
  "refresh_token": "..."
}
```

Copie o `access_token` e use-o na requisição protegida:

```http
GET http://localhost:5004/api/profile
Authorization: Bearer SEU_ACCESS_TOKEN
```

No Postman, selecione **Bearer Token** na aba **Authorization**. No Insomnia, selecione **Bearer Token** na aba **Auth**.

Para renovar o token, envie outra requisição para `/connect/token` com:

```text
client_id=dotnetauth-local
grant_type=refresh_token
refresh_token=SEU_REFRESH_TOKEN
```

## Lista de comandos

Todos os comandos abaixo são exemplos para PowerShell no Windows. O projeto usa o `Makefile` como atalho para os comandos mais frequentes.

### Ambiente

```powershell
Copy-Item .env.example .env
make dev
make infra-up
make api
make infra-status
make infra-logs
make infra-down
make infra-restart
```

`make infra-logs` acompanha os logs dos containers de infraestrutura. A API não é um serviço Docker; seus logs aparecem no terminal que executa `make api` ou `make dev`. Portanto, `docker compose logs -f api` não é um comando aplicável ao ambiente atual.

### Build e testes

```powershell
make build
make test
docker compose config --quiet
```

### Migrations

Os atalhos do `Makefile` encapsulam o projeto, o startup project e o contexto do EF Core:

```powershell
make migration-add NAME=AddClient
make migration-script
make migration-update
make migration-list
make migration-remove
```

```powershell
dotnet tool install --tool-path .tools/dotnet-ef dotnet-ef --version 9.0.10

$ef = ".tools/dotnet-ef/dotnet-ef.exe"

& $ef migrations add AddNewTable `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext `
  --output-dir IdentityAccess/Persistence/Migrations

& $ef migrations script `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext

& $ef database update `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext

& $ef migrations list `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext

& $ef migrations remove `
  --project src/DotNetAuth.Infrastructure `
  --startup-project src/DotNetAuth `
  --context DotNetAuth.Infrastructure.IdentityAccess.Persistence.ApplicationDbContext
```

### Reset de desenvolvimento

```powershell
docker compose down -v
make dev
```

O `-v` remove os volumes locais de MariaDB, Valkey, RustFS e CloudBeaver. Use-o somente quando for aceitável perder esses dados de desenvolvimento.

### Diagnóstico

```powershell
make help
make infra-status
docker compose logs -f mariadb
docker compose logs -f api
dotnet build DotNetAuth.slnx --no-restore
dotnet test DotNetAuth.slnx --no-restore
```

`docker compose logs -f mariadb` é útil para diagnosticar o banco. A linha `docker compose logs -f api` é apenas uma referência opcional e não funciona neste Compose, porque a API roda diretamente no Windows; consulte o terminal do `dotnet run`.

## Validação

Antes de abrir uma alteração, execute:

```powershell
make help
docker compose config --quiet
make build
make test
```

Para alterações de persistência, gere também o script de migration e revise o SQL antes de aplicá-lo. Para alterações de autenticação, valide o discovery OAuth, o registro, a confirmação, o fluxo Authorization Code + PKCE, a troca por token e o acesso autenticado a `/api/profile`.

As chaves OpenIddict do ambiente de desenvolvimento são efêmeras para facilitar os testes; substitua-as por certificados ou chaves persistentes antes de qualquer implantação real.

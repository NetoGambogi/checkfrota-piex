# checkfrota-piex

Sistema de gestão de frota com backend em Laravel (API) e frontend em .NET MAUI
(Android e Windows), com login via Google OAuth2 e controle de acesso por role.

- Backend: [`checkfrota-api/`](checkfrota-api) — API Laravel, autenticação Sanctum.
- Frontend: [`checkfrota-front/`](checkfrota-front) — app .NET MAUI (Android e Windows).

Este documento é o ponto de partida pra rodar os dois lados localmente. Para
detalhes de como funciona o controle de acesso por role, veja:
- [`checkfrota-api/ROLES-E-ACESSO.md`](checkfrota-api/ROLES-E-ACESSO.md) (backend)
- [`checkfrota-front/ROLES-E-ACESSO.md`](checkfrota-front/ROLES-E-ACESSO.md) (frontend, inclui como esconder/mostrar telas e botões por role)

## Pré-requisitos

| Ferramenta | Versão | Uso |
|---|---|---|
| PHP | 8.3+ | Backend |
| Composer | 2.x | Backend |
| PostgreSQL | 14+ | Banco de dados do backend |
| .NET SDK | 10.0.x | Frontend |
| Workloads MAUI | `maui-windows`, `android` (e `ios`/`maccatalyst` se for mexer nessas plataformas, exige macOS) | Frontend |
| Visual Studio 2022 (17.12+) | opcional, mas recomendado | Rodar/depurar o app MAUI mais facilmente que via CLI |

Para instalar as workloads do MAUI via CLI (sem Visual Studio):

```powershell
dotnet workload install maui android maui-windows
```

## Rodando o backend (Laravel)

```powershell
cd checkfrota-api
composer install
copy .env.example .env
php artisan key:generate
```

1. **Crie o banco Postgres localmente** com o mesmo nome/usuário/senha que estão em
   `.env` (`DB_DATABASE`, `DB_USERNAME`, `DB_PASSWORD` — o `.env.example` já vem
   com um valor padrão de desenvolvimento; ajuste se seu Postgres local usar outro
   usuário/senha).
2. **Preencha as credenciais do Google** no `.env` (peça ao responsável pelo projeto
   no Google Cloud Console, ou veja a seção [Credenciais do Google](#credenciais-do-google-oauth) abaixo):
   - `GOOGLE_CLIENT_ID` — client OAuth tipo "Web", usado pra validar o login vindo do Android.
   - `GOOGLE_CLIENT_ID_DESKTOP` / `GOOGLE_CLIENT_SECRET_DESKTOP` — client OAuth tipo
     "Desktop app", usado pelo login do MAUI no Windows.
3. Rode as migrations:
   ```powershell
   php artisan migrate
   ```
4. Suba o servidor. Use `--host=0.0.0.0` (em vez do padrão `127.0.0.1`) pra aceitar
   conexões do emulador/dispositivo Android e não só do próprio PC:
   ```powershell
   php artisan serve --host=0.0.0.0 --port=8000
   ```
5. Confirme que subiu acessando `http://localhost:8000` no navegador.

Sem esse `--host=0.0.0.0`, o app MAUI rodando no Android **não consegue** falar com
a API — foi exatamente essa a causa de um "erro inesperado ao fazer login" já visto
no projeto: o `php artisan serve` não estava rodando/acessível na rede.

## Rodando o frontend (.NET MAUI)

```powershell
cd checkfrota-front
```

### Antes de rodar no Android: ajuste o IP do backend

O app Android **não pode** usar `localhost` pra falar com a API — `localhost` dentro
do dispositivo/emulador aponta pra ele mesmo, não pro seu PC. Isso está resolvido
hoje com um IP fixo, que muda por rede/máquina, então **cada dev precisa ajustar em
dois arquivos** antes de rodar no Android:

1. Descubra o IP da sua máquina na rede local (`ipconfig`, procure o adaptador Wi-Fi/Ethernet ativo).
2. [`checkfrota-front/Services/ApiAuthService.cs`](checkfrota-front/Services/ApiAuthService.cs), constante `BaseUrl` (branch `#if ANDROID`):
   ```csharp
   private const string BaseUrl = "http://SEU_IP_AQUI:8000/api/";
   ```
3. [`checkfrota-front/Platforms/Android/Resources/xml/network_security_config.xml`](checkfrota-front/Platforms/Android/Resources/xml/network_security_config.xml)
   — troque o `<domain>` pelo mesmo IP (o Android bloqueia HTTP sem TLS por padrão;
   esse arquivo libera exceção só pra esse host):
   ```xml
   <domain includeSubdomains="false">SEU_IP_AQUI</domain>
   ```

Alternativa mais simples, **só se você for testar exclusivamente no emulador
Android** (não em dispositivo físico): use `10.0.2.2`, que é o alias fixo que o
próprio emulador reserva para "o host que está rodando o emulador" — não muda por
rede, então não precisa ajustar toda vez.

No Windows não é preciso nenhum ajuste: o app usa `http://localhost:8000/api/`
direto, porque tanto o app quanto o `php artisan serve` rodam na mesma máquina.

### Rodando

Pela CLI:

```powershell
dotnet build -t:Run -f net10.0-android           # precisa de emulador/dispositivo já conectado
dotnet build -t:Run -f net10.0-windows10.0.19041.0
```

Ou pelo Visual Studio: abra `checkfrota-front.csproj`, escolha o destino (um
emulador/dispositivo Android, ou "Windows Machine") no dropdown de execução, e
rode com F5.

### Testando o login

Use uma conta Google de teste. No **Android**, a primeira tentativa de login logo
após instalar/abrir o app pela primeira vez pode falhar com "Login cancelado." —
é um comportamento conhecido do Google Play Services "esquentando" o Credential
Manager na primeira chamada; tentar de novo em seguida funciona normalmente.

## Credenciais do Google OAuth

O projeto usa dois OAuth Client IDs diferentes no Google Cloud Console, um por
tipo de fluxo:

| Client | Tipo | Onde é usado | Onde fica configurado |
|---|---|---|---|
| Web | Web application | Validação do `id_token` vindo do login nativo no Android | `GOOGLE_CLIENT_ID` no `.env` do backend, e hardcoded em `Platforms/Android/Services/GoogleAuthService.cs` (`WebClientId`) |
| Desktop | Desktop app | Login via navegador no Windows (Authorization Code + PKCE) | `GOOGLE_CLIENT_ID_DESKTOP`/`GOOGLE_CLIENT_SECRET_DESKTOP` no `.env` do backend, e hardcoded em `Platforms/Windows/Services/GoogleAuthService.cs` (`DesktopClientId`) |

Os dois client IDs já existem no Google Cloud Console do projeto — não é preciso
criar nenhum do zero pra rodar localmente, só preencher os valores reais no
`.env`. Se o Client ID mudar (ex: for gerado um client novo), lembre de atualizar
**nos dois lugares** (backend `.env` + arquivo hardcoded do MAUI), senão a
validação do token falha com 401.

## Estrutura de roles

O sistema tem quatro roles hoje: `admin`, `motorista`, `frota` e `financeiro`.
Cada uma tem sua própria área no app depois do login. Detalhes de como estender
isso (nova rota restrita no backend, nova tela/role no frontend, esconder um
botão específico por role) estão nos dois `ROLES-E-ACESSO.md` linkados no topo
deste documento.

# checkfrota-piex

Sistema de gestão de frota com backend em Laravel (API) e frontend em .NET MAUI
(Android e Windows), com login via Google OAuth2 e controle de acesso por role.

- Backend: [`checkfrota-api/`](checkfrota-api) — API Laravel, autenticação Sanctum.
- Frontend: [`checkfrota-front/`](checkfrota-front) — app .NET MAUI (Android e Windows).

Este documento é o ponto de partida pra rodar os dois lados localmente. Para
detalhes de como funciona o controle de acesso por role, veja:
- [`checkfrota-api/ROLES-E-ACESSO.md`](checkfrota-api/ROLES-E-ACESSO.md) (backend)
- [`checkfrota-front/ROLES-E-ACESSO.md`](checkfrota-front/ROLES-E-ACESSO.md) (frontend, inclui como esconder/mostrar telas e botões por role)

Para saber quais ações geram notificação e quem recebe cada uma, veja
[`NOTIFICACOES.md`](NOTIFICACOES.md).

## Pré-requisitos

| Ferramenta | Versão | Uso |
|---|---|---|
| PHP | 8.3+ | Backend |
| Composer | 2.x | Backend |
| PostgreSQL | 14+ | Banco de dados do backend |
| .NET SDK | 10.0.x | Frontend |
| Workloads MAUI | `maui-windows`, `android` (e `ios`/`maccatalyst` se for mexer nessas plataformas, exige macOS) | Frontend |
| Visual Studio 2022 (17.12+) | opcional, mas recomendado | Rodar/depurar o app MAUI mais facilmente que via CLI |
| Projeto no Firebase | — | Notificações push no Android (ver [Notificações push](#notificações-push-firebase)) |

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
3. **Coloque a chave do Firebase** (necessária pras notificações push; sem ela a
   API funciona, mas as notificações aparecem só no sino do app). Salve o JSON da
   conta de serviço em `storage/app/private/firebase-credentials.json` e confira que
   o `.env` tem:
   ```
   FIREBASE_CREDENTIALS=storage/app/private/firebase-credentials.json
   ```
   Como gerar esse arquivo: seção [Notificações push](#notificações-push-firebase).
4. Rode as migrations:
   ```powershell
   php artisan migrate
   ```
5. Suba os **três processos** da API, cada um num terminal (todos dentro de `checkfrota-api`):

   | Terminal | Comando | Pra quê |
   |---|---|---|
   | 1 | `php artisan serve --host=0.0.0.0 --port=8000` | A API em si |
   | 2 | `php artisan queue:listen --tries=1 --timeout=0` | Fila: é ela que grava e envia as notificações |
   | 3 | `php artisan schedule:work` | Agendador: dispara o resumo diário de contas a vencer (8h, horário de Brasília) |

   Se você tem Node.js instalado, `php artisan dev` sobe os três de uma vez (e
   também o Vite). Ele usa o host do `SERVER_HOST` do `.env`, que já vem como
   `0.0.0.0` no `.env.example`.
6. Confirme que subiu acessando `http://localhost:8000` no navegador.

Sem o `--host=0.0.0.0`, o app MAUI rodando no Android **não consegue** falar com
a API — foi exatamente essa a causa de um "erro inesperado ao fazer login" já visto
no projeto: o `php artisan serve` não estava rodando/acessível na rede.

Sem o terminal 2 (fila), **nenhuma notificação é gerada**: as ações do app
funcionam normalmente, mas os avisos ficam parados na tabela `jobs` até a fila
rodar.

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

### Antes de rodar no Android: arquivo do Firebase

Pra receber notificações push, coloque o `google-services.json` do projeto Firebase em
`checkfrota-front/Platforms/Android/google-services.json`. Ele não vem no clone
(está no `.gitignore`): peça ao responsável pelo projeto ou baixe no Console do
Firebase (seção [Notificações push](#notificações-push-firebase)). Sem ele o app
compila e roda normalmente, só não recebe push.

Depois de adicionar (ou trocar) esse arquivo, faça **Clean + Rebuild**: ele só entra
no build quando existe.

### Rodando

Pela CLI:

```powershell
dotnet build -t:Run -f net10.0-android           # precisa de emulador/dispositivo já conectado
dotnet build -t:Run -f net10.0-windows10.0.19041.0
```

Ou pelo Visual Studio: abra `checkfrota-front.csproj`, escolha o destino (um
emulador/dispositivo Android, ou "Windows Machine") no dropdown de execução, e
rode com F5.

### App fechando sozinho logo depois da tela de abertura

Se o app abre a tela de abertura e fecha (no log aparece `Resources$NotFoundException`
ou um erro de `TextAppearance`/`BottomNavigationView`), o APK instalado e as DLLs
enviadas pelo Visual Studio ficaram de compilações diferentes. Acontece depois de
adicionar/remover pacotes NuGet ou arquivos em `Platforms/Android/Resources`
(ícones, cores). Pra resolver:

1. Desinstale o app do celular/emulador.
2. Apague as pastas `checkfrota-front/bin` e `checkfrota-front/obj` (com o Visual Studio fechado).
3. Compile e rode de novo.

### Testando o login

Use uma conta Google de teste. No **Android**, a primeira tentativa de login logo
após instalar/abrir o app pela primeira vez pode falhar com "Login cancelado." —
é um comportamento conhecido do Google Play Services "esquentando" o Credential
Manager na primeira chamada; tentar de novo em seguida funciona normalmente.

No Android 13+, logo depois do login o app pede permissão pra enviar notificações.
Toque em **Permitir**; se negar, dá pra liberar depois em Configurações → Apps →
CheckFrota → Notificações.

## Notificações push (Firebase)

As notificações chegam no celular mesmo com o app fechado, pelo Firebase Cloud
Messaging (FCM). Também ficam salvas no banco e aparecem no **sino** do app
(Android e Windows). O Windows não recebe push, só o sino.

```
API Laravel ──(chave da conta de serviço)──▶ Firebase ──(token do aparelho)──▶ Celular
```

São dois arquivos, ambos do **mesmo projeto Firebase**:

| Arquivo | Onde fica | Pra que serve | Pode ir pro git? |
|---|---|---|---|
| `google-services.json` | `checkfrota-front/Platforms/Android/` | Liga o app ao projeto Firebase e permite que ele obtenha o token do aparelho | Não. Está no `.gitignore`, porque o repositório é público e o arquivo tem a chave de API do projeto |
| Chave da conta de serviço | `checkfrota-api/storage/app/private/firebase-credentials.json` | Autoriza a API a enviar notificações pelo projeto | **Nunca.** Quem tem esse arquivo controla o projeto Firebase. A pasta já é ignorada pelo git |

Se o projeto Firebase já existe, peça os dois arquivos ao responsável. Pra criar
do zero:

1. Acesse <https://console.firebase.google.com> com a conta Google do projeto e
   crie um projeto (dá pra usar o mesmo projeto do Google Cloud do login). O
   Google Analytics pode ficar desativado.
2. **App Android:** "Adicionar app" → Android → nome do pacote
   **`com.companyname.checkfrotafront`** (tem que ser igual ao `ApplicationId` do
   `checkfrota-front.csproj`). SHA-1 é opcional. Baixe o `google-services.json`
   e pule as etapas de Gradle do assistente (o `.csproj` já cuida disso).
3. **Chave da API:** engrenagem → Configurações do projeto → aba **Contas de
   serviço** → **Gerar nova chave privada**. Renomeie o arquivo baixado para
   `firebase-credentials.json` e coloque no caminho da tabela acima.
4. Na aba **Cloud Messaging**, confira que a **API Firebase Cloud Messaging (V1)**
   está ativada.

### Testando

1. Suba os três processos da API (seção [Rodando o backend](#rodando-o-backend-laravel)).
2. Instale o app num celular ou num emulador **com Google Play** (imagens sem Play
   Store não recebem push), faça login e permita as notificações.
3. Confira que o aparelho foi registrado: a tabela `dispositivos` deve ter uma
   linha com o seu usuário.
4. Com **outra conta** (no Windows ou em outro aparelho), faça uma ação que gere
   aviso pro seu perfil — ex.: logado como frota no celular, registre uma viagem
   com outra conta da frota. A lista completa está em [`NOTIFICACOES.md`](NOTIFICACOES.md).
5. Pra testar o resumo de contas sem esperar as 8h: `php artisan notificacoes:contas-a-vencer`.

### Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Nada na tabela `dispositivos` depois do login | Faltou o `google-services.json` no build (Clean + Rebuild), a permissão foi negada ou o emulador não tem Google Play |
| Aparece no sino, mas não chega push | Falta o `firebase-credentials.json` ou a linha `FIREBASE_CREDENTIALS` no `.env` |
| Nem o sino atualiza | A fila não está rodando (`php artisan queue:listen`) |
| Erro 403 / `PERMISSION_DENIED` no `storage/logs/laravel.log` | API Cloud Messaging (V1) desativada, ou a chave é de outro projeto |
| `SenderId mismatch` | O `google-services.json` e a chave da API são de projetos Firebase diferentes |
| Não recebo aviso do que eu mesmo fiz | É proposital: quem faz a ação nunca é notificado dela |

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

O sistema tem quatro roles hoje: `admin`, `motorista`, `frota` e `financeiro`
(além de `pendente`, o estado de quem entrou pela primeira vez e espera aprovação).
Cada uma tem sua própria área no app depois do login. Detalhes de como estender
isso (nova rota restrita no backend, nova tela/role no frontend, esconder um
botão específico por role) estão nos dois `ROLES-E-ACESSO.md` linkados no topo
deste documento.

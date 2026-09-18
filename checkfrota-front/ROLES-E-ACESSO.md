# Roles e páginas por role (front-end)

## Como a navegação por role funciona hoje

O app usa **.NET MAUI Shell**. Cada "área" de role é um `ShellContent` de topo,
declarado em [AppShell.xaml](AppShell.xaml):

```xml
<ShellContent Title="Login" ContentTemplate="{DataTemplate views:LoginPage}" Route="login" />
<ShellContent Title="Pendente" ContentTemplate="{DataTemplate views:PendingApprovalPage}" Route="pendente" />
<ShellContent Title="Admin" ContentTemplate="{DataTemplate views:AdminHomePage}" Route="admin" />
<ShellContent Title="Motorista" ContentTemplate="{DataTemplate views:MotoristaHomePage}" Route="motorista" />
<ShellContent Title="Frota" ContentTemplate="{DataTemplate views:FrotaHomePage}" Route="frota" />
<ShellContent Title="Financeiro" ContentTemplate="{DataTemplate views:FinanceiroHomePage}" Route="financeiro" />
```

`pendente` é um caso especial: não é uma área com telas próprias, é só a tela
"Aguardando aprovação" (`PendingApprovalPage`) que todo usuário novo vê até um
admin atribuir a role definitiva no backend (ver `ROLES-E-ACESSO.md` do
`checkfrota-api`). O botão "Voltar para login" nela faz logout e volta pra
`//login` — a pessoa precisa logar de novo depois que a role for atribuída pra
cair na área certa.

Depois do login, [ViewModels/LoginViewModel.cs](ViewModels/LoginViewModel.cs) decide
pra onde navegar com base na `role` que a API devolveu:

```csharp
var route = login.User.Role switch
{
    "admin" => "//admin",
    "motorista" => "//motorista",
    "frota" => "//frota",
    "financeiro" => "//financeiro",
    "pendente" => "//pendente",
    _ => throw new InvalidOperationException($"Role desconhecida: {login.User.Role}")
};

await Shell.Current.GoToAsync(route);
```

O `//` no início significa "navegação absoluta pra esse item de topo do Shell" —
troca a área inteira, não empilha página. **Toda role que a API pode devolver
precisa ter um `case` aqui**, senão o login quebra com `InvalidOperationException`.

Cada página tem seu próprio ViewModel (`AdminHomeViewModel`,
`MotoristaHomeViewModel`), registrados como `Transient` no container de DI em
[MauiProgram.cs](MauiProgram.cs) — página e ViewModel sempre andam juntos nesse
registro.

## Como adicionar uma página nova dentro de uma role já existente

Exemplo: uma tela de "Relatórios" que só o admin acessa, aberta a partir da
`AdminHomePage`.

1. **ViewModel** — crie `ViewModels/RelatoriosViewModel.cs` seguindo o padrão de
   `AdminHomeViewModel.cs` (injete `ApiAuthService` ou outro serviço que precisar).
2. **Página** — crie `Views/RelatoriosPage.xaml` + `.xaml.cs`, também no mesmo
   padrão (`x:DataType` apontando pro ViewModel, construtor recebendo o ViewModel
   via DI e setando `BindingContext`).
3. **Registrar no DI**, em `MauiProgram.cs`:
   ```csharp
   builder.Services.AddTransient<RelatoriosViewModel>();
   builder.Services.AddTransient<Views.RelatoriosPage>();
   ```
4. **Registrar a rota.** Como essa página não é um item de topo do Shell (não tem
   menu próprio, é só uma tela que o admin abre e volta), ela não entra como
   `ShellContent` no XAML — em vez disso, registre a rota dinamicamente no
   construtor de [AppShell.xaml.cs](AppShell.xaml.cs):
   ```csharp
   public AppShell()
   {
       InitializeComponent();
       Routing.RegisterRoute("relatorios", typeof(Views.RelatoriosPage));
   }
   ```
5. **Navegar até ela** a partir de `AdminHomePage`/`AdminHomeViewModel`, com rota
   **relativa** (sem `//`), que empilha a página sobre a área atual:
   ```csharp
   await Shell.Current.GoToAsync("relatorios");
   ```
   (`//admin/relatorios` também funciona e é mais explícito sobre a hierarquia,
   mas o relativo já resolve dentro da área atual.)

Regra prática: **rota absoluta (`//nome`) troca de área/role; rota relativa
(`nome`) empilha uma tela dentro da área atual.**

## Como criar uma role nova (ex: "gestor")

Isso sempre é coordenado com o backend — veja também `ROLES-E-ACESSO.md` no
repositório do `checkfrota-api`. Do lado do front:

1. Crie `ViewModels/GestorHomeViewModel.cs` e `Views/GestorHomePage.xaml(.cs)`,
   copiando o padrão de `MotoristaHomeViewModel`/`MotoristaHomePage`.
2. Registre os dois no DI em `MauiProgram.cs`:
   ```csharp
   builder.Services.AddTransient<GestorHomeViewModel>();
   builder.Services.AddTransient<Views.GestorHomePage>();
   ```
3. Adicione o `ShellContent` de topo em `AppShell.xaml`:
   ```xml
   <ShellContent Title="Gestor" ContentTemplate="{DataTemplate views:GestorHomePage}" Route="gestor" />
   ```
4. Adicione o `case` correspondente no `switch` de `LoginViewModel.cs`:
   ```csharp
   "gestor" => "//gestor",
   ```
5. Garanta que o backend realmente pode devolver `"role": "gestor"` pra algum
   usuário (ver documento do backend) — sem isso, ninguém nunca cai nesse caso.

## Mostrando/escondendo uma tela ou botão só para uma role específica

Existem dois níveis de controle de acesso no front-end, dependendo do que você
precisa:

1. **Tela inteira exclusiva de uma role** → já é o caso de `AdminHomePage`,
   `MotoristaHomePage`, `FrotaHomePage` e `FinanceiroHomePage`: cada uma é sua
   própria área (`ShellContent`), e só quem tem aquela role é roteado pra lá no
   login (veja o `switch` em `LoginViewModel.cs` acima). Ninguém "cai" numa área
   de outra role sem que o `LoginViewModel` mande pra lá.
2. **Botão/seção específica dentro de uma tela compartilhada por várias roles**
   → é o caso comum de "essa página todo mundo vê, mas só o financeiro vê esse
   botão aqui". Pra isso, a role do usuário logado fica salva localmente
   (`SecureStorage`, gravada por `ApiAuthService.SaveSessionAsync` logo após um
   login bem-sucedido) e pode ser lida a qualquer momento:

   ```csharp
   var role = await SecureStorage.GetAsync("user_role");
   ```

### Exemplo prático: botão visível só para `financeiro`

Suponha uma página compartilhada (ex: um dashboard que `admin` e `financeiro`
acessam) onde só o `financeiro` deve ver um botão "Exportar relatório
financeiro". No ViewModel dessa página, exponha uma propriedade booleana
calculada a partir da role, atualizada no `AppearingCommand` (o mesmo padrão já
usado pra carregar `UserName` em `AdminHomeViewModel`/`MotoristaHomeViewModel`):

```csharp
public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isFinanceiro;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var role = await SecureStorage.GetAsync("user_role");
        IsFinanceiro = role == "financeiro";
    }
}
```

E no XAML, ligue a visibilidade do botão a essa propriedade:

```xml
<Button Text="Exportar relatório financeiro"
        IsVisible="{Binding IsFinanceiro}"
        Command="{Binding ExportarRelatorioCommand}" />
```

Se precisar liberar pra mais de uma role (ex: `admin` **ou** `financeiro`), o
mesmo padrão funciona, só muda a comparação:

```csharp
IsFinanceiro = role is "admin" or "financeiro";
```

**Importante**: isso é controle de **exibição**, não de segurança — esconder um
botão no app não impede alguém de chamar o endpoint diretamente. A proteção de
verdade é sempre no backend, via `role:financeiro` nas rotas (veja o
`ROLES-E-ACESSO.md` do `checkfrota-api`). Trate o `IsVisible` como UX, não como
controle de acesso.

## Testando

Não tem testes de UI automatizados no projeto. Ao adicionar uma página/role nova,
o mínimo pra validar manualmente:

- `dotnet build -f net10.0-android` sem erros;
- login com um usuário de cada role e conferir que cai na tela certa;
- se a role for nova, testar também o caso de uma role *desconhecida* vinda da
  API (deve mostrar a mensagem de erro do `LoginViewModel`, não travar o app).

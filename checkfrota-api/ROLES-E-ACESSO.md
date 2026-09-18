# Roles e controle de acesso (backend)

Este projeto não tem uma tabela de "roles" separada. Cada usuário tem uma coluna
`role` (string) direto na tabela `users`, definida em
[database/migrations/0001_01_01_000000_create_users_table.php](database/migrations/0001_01_01_000000_create_users_table.php):

```php
$table->string('role')->default('motorista');
```

Hoje o sistema usa cinco roles: `admin`, `motorista`, `frota`, `financeiro` e
`pendente`. Não existe uma lista fixa/enum validando os valores — qualquer string
gravada nessa coluna funciona como role.

`pendente` é a role padrão de todo usuário novo (ver
[app/Http/Controllers/AuthController.php](app/Http/Controllers/AuthController.php),
`issueSession`): quem faz o primeiro login via Google entra nessa role até um
`admin` atribuir a role definitiva. Não é uma role "normal" com rotas próprias —
é um estado de espera; a tela que o front-end mostra pra ela é só informativa
(ver `ROLES-E-ACESSO.md` do front).

### Contas removidas (soft delete)

A tabela `users` usa `SoftDeletes` (`deleted_at`). Um usuário indesejado deve ser
removido com `$user->delete()` (nunca `forceDelete()`), o que:

- some da listagem normal (qualquer `User::where(...)` já ignora registros
  removidos automaticamente, por causa do global scope do `SoftDeletes`);
- **bloqueia login futuro**: `AuthController::issueSession` busca o usuário com
  `withTrashed()` e, se encontrar um registro removido (`$user->trashed()`),
  aborta com 403 em vez de deixar logar ou criar uma conta nova com o mesmo
  e-mail/`google_id`. Sem o `withTrashed()` aqui, a busca padrão não acharia o
  registro removido e tentaria criar um novo — e bateria na constraint `unique`
  de `email`/`google_id`.

Pra reativar (funcionário recontratado, remoção por engano, etc.), restaure o
registro em vez de recriar o usuário:

```
php artisan tinker --execute='App\Models\User::withTrashed()->where("email", "fulano@empresa.com")->restore();'
```

Isso preserva o histórico (id, tokens antigos revogados, etc.) e evita duplicar
a linha em `users`.

## Como o acesso é checado

O middleware [app/Http/Middleware/CheckRole.php](app/Http/Middleware/CheckRole.php)
recebe a role exigida como parâmetro e bloqueia (403) se o usuário autenticado não
tiver exatamente essa role:

```php
public function handle($request, Closure $next, string $role)
{
    if ($request->user()->role !== $role) {
        abort(403, 'Acesso negado.');
    }
    return $next($request);
}
```

Ele é registrado com o alias `role` em [bootstrap/app.php](bootstrap/app.php):

```php
$middleware->alias([
    'role' => CheckRole::class,
]);
```

Isso é o que permite escrever `Route::middleware('role:admin')` nas rotas.

**Limitação atual**: o middleware só aceita **uma** role por rota. Se um dia precisar
de uma rota liberada para mais de uma role (ex: `admin` e `gestor`), o jeito mais
simples é alterar `CheckRole::handle` pra aceitar múltiplos parâmetros:

```php
public function handle($request, Closure $next, string ...$roles)
{
    if (! in_array($request->user()->role, $roles, true)) {
        abort(403, 'Acesso negado.');
    }
    return $next($request);
}
```

e usar como `role:admin,gestor`.

## Como adicionar uma rota só para uma role

Todas as rotas da API ficam em [routes/api.php](routes/api.php). O padrão já
estabelecido é: autenticação com Sanctum primeiro, depois a role.

```php
Route::middleware('auth:sanctum')->group(function () {
    Route::get('/me', [AuthController::class, 'me']);

    Route::middleware('role:admin')->group(function () {
        // Rotas só de admin. Exemplo:
        Route::get('/frota', [FrotaController::class, 'index']);
        Route::post('/frota', [FrotaController::class, 'store']);
    });

    Route::middleware('role:motorista')->group(function () {
        // Rotas só de motorista. Exemplo:
        Route::post('/rotas/registrar', [RotaController::class, 'registrar']);
    });
});
```

Passos práticos pra adicionar uma rota nova:

1. Crie o controller com `php artisan make:controller NomeController --no-interaction`
   (siga o padrão de `AuthController`: métodos tipados, retornando `JsonResponse`).
2. Adicione a rota dentro do bloco `role:admin` ou `role:motorista` já existente
   em `routes/api.php` (ou crie um novo bloco `role:outraRole` se for o caso).
3. Rode `php artisan route:list --path=api` pra confirmar que a rota apareceu.

## Como criar uma role nova

Não tem "cadastro de role" — criar uma role nova é decidir uma nova string e:

1. **Atribuir a role a um usuário.** Algumas formas:
   - Pré-cadastrar antes do primeiro login Google (recomendado para roles
     administrativas): crie o usuário manualmente com a role já definida.
     `AuthController::loginWithGoogle` busca por `email` OU `google_id` antes de
     criar um novo registro — então, se você já criar a linha em `users` com o
     e-mail certo e `role` definida, o primeiro login via Google daquela pessoa só
     vincula o `google_id`, sem sobrescrever a role.
   - Via Tinker, pra um usuário que já existe:
     ```
     php artisan tinker --execute='App\Models\User::where("email", "fulano@empresa.com")->update(["role" => "gestor"]);'
     ```
   - Via seeder/factory em testes: `User::factory()->create(['role' => 'gestor'])`
     (veja o método `admin()` em
     [database/factories/UserFactory.php](database/factories/UserFactory.php) como
     modelo pra criar um state equivalente, ex: `gestor()`).
2. **Proteger rotas com essa role** usando `role:gestor` em `routes/api.php`, como
   no exemplo acima.
3. Não esqueça do lado do app: o front-end (`checkfrota-front`) também precisa saber
   pra onde navegar quando a API devolve essa role. Veja `ROLES-E-ACESSO.md` no
   repositório do front pra esse passo.

## Testando

O arquivo [tests/Feature/AuthControllerTest.php](tests/Feature/AuthControllerTest.php)
tem exemplos de teste do fluxo de login/roles, incluindo o cenário de usuário
pré-provisionado por e-mail. Ao adicionar rotas novas restritas por role, vale
adicionar um teste garantindo que:

- a role certa consegue acessar (200/2xx);
- outra role qualquer recebe 403;
- sem autenticação recebe 401.

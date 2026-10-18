# Notificações

Quais ações do sistema geram notificação, quem recebe e o que acontece ao tocar
nela. Para configurar o envio (Firebase, fila, agendador), veja a seção
[Notificações push](README.md#notificações-push-firebase) do README.

## Como funciona

Toda notificação é entregue de duas formas:

- **Push no celular (Android):** chega na barra de notificações mesmo com o app
  fechado, pelo Firebase. Vai para todos os aparelhos em que o usuário está logado.
- **Sino do app (Android e Windows):** fica salva no banco e aparece no sino da
  barra superior, com o número de não lidas. A tela de notificações lista as 50
  mais recentes.

Regras que valem para todas:

- **Quem fez a ação não recebe aviso dela.** Se um admin lança uma manutenção, os
  outros admins e o financeiro são avisados, mas ele não.
- **Usuários removidos não recebem nada**, e um aviso que só iria para eles não é
  enviado (ex.: pagar uma conta lançada por um usuário que já foi removido).
- O envio passa pela fila (`php artisan queue:listen`). Com a fila parada, nada é
  gerado até ela voltar a rodar.
- Ao **sair da conta**, o aparelho para de receber push daquele usuário.

## Ações que geram notificação

### Frota → financeiro

| Ação | Quem recebe | Título | Exemplo de mensagem |
|---|---|---|---|
| Lançar uma **manutenção** | Financeiro e admins | "Nova conta a pagar" (ou "Despesa paga lançada", se já foi lançada como paga) | Neto lançou Manutenção ABC1D23 - Troca de óleo: R$ 450,90, vence em 20/10/2026. |
| Lançar um **abastecimento** | Financeiro e admins | "Nova conta a pagar" (ou "Despesa paga lançada") | Neto lançou Abastecimento ABC1D23 - …: R$ 600,00, vence em 01/10/2026. |
| Registrar uma **viagem com valor de frete** | Financeiro e admins | "Nova receita a receber" (ou "Receita recebida lançada") | Neto lançou Frete Curitiba x Joinville - ABC1D23 (Mercado Bom): R$ 4.200,00, vence em 02/10/2026. |
| **Informar o frete** ao editar uma viagem que não tinha frete | Financeiro e admins | "Nova receita a receber" | Igual ao anterior |

Ao tocar, o financeiro e os admins vão para **Lançamentos**, já filtrado nos
pendentes.

### Viagens → frota

| Ação | Quem recebe | Título | Exemplo de mensagem |
|---|---|---|---|
| Registrar uma viagem **sem data de chegada** | Frota e admins | "Nova viagem iniciada" | ABC1D23 - Curitiba x Joinville (Curitiba/PR → Joinville/SC) para Mercado Bom, saída em 01/10/2026. |
| Registrar uma viagem **já com data de chegada** | Frota e admins | "Viagem concluída" | ABC1D23 - Curitiba x Joinville (…) para Mercado Bom, chegada em 02/10/2026, 230 km rodados. |
| **Informar a data de chegada** ao editar uma viagem em andamento | Frota e admins | "Viagem concluída" | Igual ao anterior |

O "km rodados" só aparece quando o km de saída e o de chegada foram informados.
Editar de novo uma viagem que já estava concluída não gera outro aviso. Ao tocar,
abre a tela de **Viagens**.

### Financeiro → quem lançou

| Ação | Quem recebe | Título | Exemplo de mensagem |
|---|---|---|---|
| Tocar em **"Marcar como pago"** num lançamento | Quem criou o lançamento (ex.: o gestor de frota que lançou a manutenção) | "Despesa paga" ou "Receita recebida" | Manutenção ABC1D23 - Troca de óleo (R$ 450,90) foi paga em 05/10/2026. |

Só o botão **"Marcar como pago"** registra o pagamento. Editar o lançamento e
tocar em "Salvar" não muda o status nem gera aviso. Ao tocar no aviso, abre a
lista de notificações.

### Acesso de usuários

| Ação | Quem recebe | Título | Exemplo de mensagem |
|---|---|---|---|
| Alguém entra no app **pela primeira vez** | Admins | "Novo usuário aguardando aprovação" | Maria (maria@empresa.com) entrou no app e precisa de um perfil de acesso. |
| Um admin muda a role de um usuário **de pendente para outra** | O próprio usuário | "Acesso liberado" | Seu acesso foi aprovado com o perfil gestão de frota. Entre novamente no app para começar. |

Ao tocar no primeiro, o admin vai para a aba **Usuários**. Trocar a role de
quem já tinha acesso (ex.: de frota para financeiro) não gera aviso.

## Aviso automático: resumo de contas

Todo dia às **8h (horário de Brasília)** o agendador junta os lançamentos
**pendentes** que já venceram, que vencem hoje ou que vencem nos próximos 3 dias,
e manda **um único resumo** por pessoa.

| Quem recebe | Título | Exemplo de mensagem |
|---|---|---|
| Financeiro e admins | "Contas a vencer" (ou "Contas vencidas e a vencer", se houver alguma vencida) | A pagar: 1 vencida (R$ 300,00), 2 vencem hoje (R$ 1.250,00). A receber: 1 nos próximos 3 dias (R$ 4.000,00). |

- Inclui despesas (a pagar) e receitas (a receber), inclusive parcelas de financiamentos.
- Se não houver nada vencido nem vencendo, ninguém recebe aviso naquele dia.
- Ao tocar, abre **Lançamentos** filtrado nos pendentes.
- Para rodar na hora, sem esperar as 8h: `php artisan notificacoes:contas-a-vencer`
  (aceita `--dias=7` para olhar mais dias à frente).

## O que não gera notificação

- Lançamentos criados direto pelo financeiro na tela de Lançamentos.
- Editar ou excluir manutenções, abastecimentos, lançamentos e viagens (exceto
  informar a chegada ou o frete, descritos acima).
- Cadastros de veículos, rotas, categorias e formas de pagamento.
- Trocar a role de quem já tinha acesso, ou remover/reativar usuários.
- A role **motorista** hoje não recebe nenhum aviso: ela ainda não tem telas nem
  dados ligados a ela.

## Para quem vai mexer no código

| O quê | Onde |
|---|---|
| Classes das notificações (título, mensagem, tela de destino) | `checkfrota-api/app/Notifications/` (todas estendem `NotificacaoApp`) |
| Onde cada aviso é disparado | `ManutencaoController`, `AbastecimentoController`, `ViagemController`, `MovimentacaoFinanceiraController::pagar`, `AuthController`, `UserController::updateRole` |
| Quem recebe por role | `User::destinatarios()` em `checkfrota-api/app/Models/User.php` |
| Resumo diário e horário | `checkfrota-api/app/Console/Commands/NotificarContasAVencerCommand.php` e `checkfrota-api/routes/console.php` |
| Envio ao Firebase | `checkfrota-api/app/Services/FirebaseMessagingService.php` e `app/Notifications/Channels/FcmChannel.php` |
| Tela aberta ao tocar, por role | `RotaDoDestinoAsync` em `checkfrota-front/Services/NotificacaoService.cs` |
| Recebimento do push no Android | `checkfrota-front/Platforms/Android/Services/` e `Platforms/Android/MainActivity.cs` |
| Testes | `checkfrota-api/tests/Feature/NotificacaoTest.php` |

Para criar um aviso novo: crie uma classe em `app/Notifications/` estendendo
`NotificacaoApp` (implemente `titulo()`, `mensagem()` e, se tiver tela própria,
`destino()`), dispare com `Notification::send(User::destinatarios([...], $autor), new SuaNotificacao(...))`
no ponto da ação e, se o `destino` for novo, mapeie a tela em `RotaDoDestinoAsync`
no app.

<?php

namespace Database\Seeders;

use App\Models\CategoriaFinanceira;
use App\Models\FormaPagamento;
use App\Models\Rota;
use App\Models\Veiculo;
use Database\Seeders\Concerns\LinhaDoTempo;
use Illuminate\Database\Seeder;

/**
 * Cadastros base: categorias financeiras, formas de pagamento, veículos e rotas, criados pouco
 * antes do início do histórico.
 */
class CadastroSeeder extends Seeder
{
    use LinhaDoTempo;

    /**
     * Frota da transportadora. `km`: hodômetro no início do histórico; `media`: km/l base;
     * `tendencia`: variação da média por mês (o Actros piora com o tempo); `corretiva`: chance (%)
     * de ocorrência/defeito por viagem; `rotas`: índices de {@see ROTAS}, repetidos para pesar o sorteio;
     * `motorista`: titular (o Volkswagen troca de motorista no meio do histórico).
     *
     * @var list<array{placa: string, tipo: string, marca: string, modelo: string, ano: int, renavam: string, km: int, media: float, tendencia: float, corretiva: int, rotas: list<int>, motorista: string}>
     */
    public const VEICULOS = [
        ['placa' => 'RHA4B21', 'tipo' => 'caminhao', 'marca' => 'Volvo', 'modelo' => 'FH 540', 'ano' => 2022, 'renavam' => '01284567893', 'km' => 182000, 'media' => 2.7, 'tendencia' => 0.0, 'corretiva' => 5, 'rotas' => [0, 1, 1, 2, 5], 'motorista' => 'carlos.almeida'],
        ['placa' => 'SBC7D45', 'tipo' => 'caminhao', 'marca' => 'Scania', 'modelo' => 'R 450', 'ano' => 2023, 'renavam' => '01325478960', 'km' => 95000, 'media' => 2.95, 'tendencia' => 0.01, 'corretiva' => 3, 'rotas' => [0, 0, 1, 2], 'motorista' => 'ana.souza'],
        ['placa' => 'QWE3F88', 'tipo' => 'caminhao', 'marca' => 'Mercedes-Benz', 'modelo' => 'Actros 2651', 'ano' => 2017, 'renavam' => '01098765432', 'km' => 410000, 'media' => 2.55, 'tendencia' => -0.025, 'corretiva' => 14, 'rotas' => [0, 1, 4, 4, 5], 'motorista' => 'rafael.martins'],
        ['placa' => 'RTY2G19', 'tipo' => 'utilitario', 'marca' => 'Volkswagen', 'modelo' => 'Delivery 11.180', 'ano' => 2021, 'renavam' => '01234098761', 'km' => 130000, 'media' => 5.4, 'tendencia' => 0.0, 'corretiva' => 3, 'rotas' => [2, 3, 3, 4], 'motorista' => 'bruno.ferreira'],
        ['placa' => 'SDF9H63', 'tipo' => 'caminhao', 'marca' => 'DAF', 'modelo' => 'XF 480', 'ano' => 2024, 'renavam' => '01376543218', 'km' => 21000, 'media' => 3.05, 'tendencia' => 0.0, 'corretiva' => 2, 'rotas' => [0, 1, 2, 5, 5], 'motorista' => 'juliana.costa'],
    ];

    /**
     * `frete_km`: valor médio do frete por km da rota.
     *
     * @var list<array{nome: string, origem: string, destino: string, km: int, frete_km: float}>
     */
    public const ROTAS = [
        ['nome' => 'Curitiba x São Paulo', 'origem' => 'Curitiba/PR', 'destino' => 'São Paulo/SP', 'km' => 410, 'frete_km' => 5.6],
        ['nome' => 'Cascavel x Paranaguá', 'origem' => 'Cascavel/PR', 'destino' => 'Paranaguá/PR', 'km' => 600, 'frete_km' => 4.4],
        ['nome' => 'Ponta Grossa x Joinville', 'origem' => 'Ponta Grossa/PR', 'destino' => 'Joinville/SC', 'km' => 230, 'frete_km' => 6.2],
        ['nome' => 'Londrina x Maringá', 'origem' => 'Londrina/PR', 'destino' => 'Maringá/PR', 'km' => 100, 'frete_km' => 7.5],
        ['nome' => 'Foz do Iguaçu x Cascavel', 'origem' => 'Foz do Iguaçu/PR', 'destino' => 'Cascavel/PR', 'km' => 145, 'frete_km' => 4.8],
        ['nome' => 'Maringá x Campinas', 'origem' => 'Maringá/PR', 'destino' => 'Campinas/SP', 'km' => 620, 'frete_km' => 4.9],
    ];

    /** @var array<string, array{tipo: string, icone: string, descricao: string}> */
    private const CATEGORIAS = [
        'Combustível' => ['tipo' => 'despesa', 'icone' => 'combustivel', 'descricao' => 'Diesel dos caminhões e utilitários'],
        'Manutenção' => ['tipo' => 'despesa', 'icone' => 'manutencao', 'descricao' => 'Revisões, peças, pneus e serviços de oficina'],
        'Pedágio' => ['tipo' => 'despesa', 'icone' => 'pedagio', 'descricao' => 'Fatura mensal da tag de pedágio'],
        'Seguro da frota' => ['tipo' => 'despesa', 'icone' => 'seguro', 'descricao' => 'Parcelas do seguro dos veículos'],
        'Salários' => ['tipo' => 'despesa', 'icone' => 'outros', 'descricao' => 'Folha de pagamento da equipe'],
        'Impostos e taxas' => ['tipo' => 'despesa', 'icone' => 'outros', 'descricao' => 'DAS, IPVA, licenciamento e multas'],
        'Financiamento de veículos' => ['tipo' => 'despesa', 'icone' => 'outros', 'descricao' => 'Parcelas dos veículos financiados'],
        'Despesas administrativas' => ['tipo' => 'despesa', 'icone' => 'outros', 'descricao' => 'Contabilidade, sistemas e escritório'],
        'Fretes' => ['tipo' => 'receita', 'icone' => 'outros', 'descricao' => 'Receita dos fretes realizados'],
        'Locação de veículos' => ['tipo' => 'receita', 'icone' => 'outros', 'descricao' => 'Aluguel de veículos ociosos para terceiros'],
    ];

    private const FORMAS_PAGAMENTO = ['Pix', 'Boleto', 'Transferência', 'Cartão de crédito', 'Cartão combustível', 'Dinheiro'];

    public function run(): void
    {
        $this->noMomento($this->as($this->inicioHistorico()->subDays(20), 10), function () {
            foreach (self::CATEGORIAS as $nome => $dados) {
                CategoriaFinanceira::create(['nome' => $nome, ...$dados]);
            }

            foreach (self::FORMAS_PAGAMENTO as $nome) {
                FormaPagamento::create(['nome' => $nome]);
            }

            foreach (self::ROTAS as $dados) {
                Rota::create([
                    'nome' => $dados['nome'],
                    'origem' => $dados['origem'],
                    'destino' => $dados['destino'],
                    'km_aproximado' => $dados['km'],
                ]);
            }

            foreach (self::VEICULOS as $dados) {
                Veiculo::create([
                    ...collect($dados)->only(['placa', 'tipo', 'marca', 'modelo', 'ano', 'renavam'])->all(),
                    'km_atual' => $dados['km'],
                ]);
            }
        });
    }
}

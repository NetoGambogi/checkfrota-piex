<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>CheckFrota API</title>
    <link rel="icon" href="/favicon.ico">
    <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5.17.14/swagger-ui.css">
    <style>
        :root {
            --header-bg: #0A1330;
            --accent: #3D4FE0;
        }

        body {
            margin: 0;
            background: #FFFFFF;
            font-family: "Open Sans", system-ui, sans-serif;
        }

        .cf-header {
            background: var(--header-bg);
            color: #FFFFFF;
            padding: 18px 24px;
            display: flex;
            align-items: baseline;
            gap: 12px;
            flex-wrap: wrap;
        }

        .cf-header h1 {
            margin: 0;
            font-size: 20px;
            font-weight: 700;
        }

        .cf-header span {
            opacity: 0.7;
            font-size: 14px;
        }

        .swagger-ui .topbar {
            display: none;
        }

        .swagger-ui .info .title,
        .swagger-ui .opblock-tag {
            color: var(--header-bg);
        }

        .swagger-ui .btn.authorize {
            color: var(--accent);
            border-color: var(--accent);
        }

        .swagger-ui .btn.authorize svg {
            fill: var(--accent);
        }
    </style>
</head>
<body>
    <header class="cf-header">
        <h1>CheckFrota API</h1>
        <span>Documentação dos endpoints · use “Authorize” com o token do login</span>
    </header>

    <div id="swagger-ui"></div>

    <script src="https://unpkg.com/swagger-ui-dist@5.17.14/swagger-ui-bundle.js" crossorigin></script>
    <script>
        // O servidor em que a página está aberta (VPS ou local) vira a opção padrão do "Try it out".
        fetch('/openapi.json')
            .then((resposta) => resposta.json())
            .then((spec) => {
                const atual = window.location.origin + '/api';
                spec.servers = [
                    { url: atual, description: 'Este servidor' },
                    ...spec.servers.filter((servidor) => servidor.url !== atual),
                ];

                SwaggerUIBundle({
                    spec,
                    dom_id: '#swagger-ui',
                    deepLinking: true,
                    persistAuthorization: true,
                    docExpansion: 'none',
                    filter: true,
                    tagsSorter: (a, b) => spec.tags.findIndex((t) => t.name === a) - spec.tags.findIndex((t) => t.name === b),
                    operationsSorter: 'method',
                    tryItOutEnabled: false,
                    requestInterceptor: (requisicao) => {
                        requisicao.headers.Accept = 'application/json';
                        return requisicao;
                    },
                });
            });
    </script>
</body>
</html>

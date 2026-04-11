# Route53DDNS

Um utilitário em .NET 8 para atualização dinâmica de registros do AWS Route 53 com o seu IP público atual, funcionando de forma similar a um serviço de DNS Dinâmico (DDNS).

---

## 🚀 Propósito e Contexto

O **Route53DDNS** foi desenvolvido para resolver o problema de hosts em redes domésticas ou pequenos escritórios que possuem IPs públicos dinâmicos, mas precisam ser acessados via domínios gerenciados no AWS Route 53.

A ferramenta monitora continuamente o IP externo da rede (via `api.ipify.org`) e, ao detectar uma alteração, sincroniza automaticamente os registros configurados na AWS.

## 🛠️ Tecnologias Utilizadas

- **Linguagem:** C# 12
- **Framework:** .NET 8.0
- **SDK AWS:** `AWSSDK.Route53`
- **Resiliência:** `Polly` (para retentativas de chamadas HTTP)
- **Testes:** xUnit e `NSubstitute` (Mocking)
- **Containerização:** Docker (Alpine/Aspnet runtime)

## ⚙️ Configuração

O projeto pode ser configurado de duas formas principais: via arquivo `config.json` ou Variáveis de Ambiente.

### 1. Arquivo `config.json`

Crie um arquivo chamado `config.json` no diretório raiz da aplicação:

```json
{
  "accessKey": "sua_aws_access_key",
  "secretKey": "sua_aws_secret_key",
  "targetHostedZone": "exemplo.com",
  "targetRecords": [
    {
      "name": "home.exemplo.com",
      "type": "A"
    },
    {
      "name": "*.home.exemplo.com",
      "type": "A"
    }
  ],
  "interval": 60
}
```

- `accessKey`/`secretKey`: Credenciais do IAM com permissão `route53:ChangeResourceRecordSets` e `route53:ListHostedZones`.
- `targetHostedZone`: O nome da zona hospedada no Route 53.
- `targetRecords`: Lista de registros que devem ser atualizados.
- `interval`: Intervalo de checagem em segundos.

### 2. Variáveis de Ambiente

As credenciais da AWS podem ser passadas via ambiente, o que é preferível para execução em containers:

- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`

Se essas variáveis estiverem presentes, elas terão prioridade sobre o `config.json`.

## 📦 Como Buildar e Rodar

### Execução Local (.NET SDK)

1. Clone o repositório.
2. Configure o `config.json`.
3. Restaure as dependências:
   ```bash
   dotnet restore
   ```
4. Build do projeto:
   ```bash
   dotnet build -c Release
   ```
5. Rode a aplicação:
   ```bash
   dotnet run --project Route53DDns/Route53DDns.csproj
   ```

### Execução via Docker

A aplicação possui um `Dockerfile` multi-estágio para otimização de imagem.

1. Build da imagem:
   ```bash
   docker build -t route53-ddns .
   ```
2. Rodar o container (mapeando o config):
   ```bash
   docker run -d \
     -v ${PWD}/Route53DDns/config.json:/app/config.json \
     --name ddns-updater \
     route53-ddns
   ```
   *Ou passando credenciais via ambiente:*
   ```bash
   docker run -d \
     -e AWS_ACCESS_KEY_ID=XXX \
     -e AWS_SECRET_ACCESS_KEY=YYY \
     -v ${PWD}/Route53DDns/config.json:/app/config.json \
     route53-ddns
   ```

## 🧪 Testes

O projeto conta com uma suíte de testes unitários cobrindo a lógica de configuração, validação de IP e orquestração do Route 53.

Para rodar os testes:
```bash
dotnet test
```

## 🛡️ Segurança e Permissões

Recomenda-se criar um usuário IAM com permissões mínimas (Princípio do Menor Privilégio). Exemplo de política necessária:

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "route53:ListHostedZones",
                "route53:ListResourceRecordSets",
                "route53:ChangeResourceRecordSets"
            ],
            "Resource": "*"
        }
    ]
}
```

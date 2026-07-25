# Plano de Implementação: Modificador de Nomes Customizável (Item 1.1)

Este plano concentra-se exclusivamente na implementação da ferramenta do Modificador de Nomes Personalizado. O mecanismo adotado utilizará um campo de texto principal onde o usuário poderá digitar livremente e inserir tags (tokens) clicando em botões.

## Goal Description

Atualmente o Modificador de Nomes suporta os estados "Padrão" (Default) ou "Desativado" (Disabled). O objetivo desta sprint é tornar a opção "Personalizado" funcional e amigável.
O usuário utilizará o botão **"Personalizar"** (já existente no menu superior) para abrir a nova interface.

### O Modelo (Exemplo):
O usuário poderá digitar livremente e adicionar tokens. Por exemplo, se ele quiser adicionar a letra "C" no início:
`C{Simbolo}{Nominal} {Tolerancia}`

- **Caracteres estáticos:** O usuário pode simplesmente digitar letras (como "C"), espaços ou símbolos customizados diretamente na caixa de texto.
- **{Simbolo}:** Será substituído pelo símbolo da característica (ex: Ø, °). Se não for identificado nenhum, ficará vazio.
- **{Nominal}:** Campo obrigatório. Valor nominal formatado.
- **{Tolerancia}:** Respeitará a regra de negócio atual automaticamente:
  - Se absolutos Sup e Inf forem iguais: ex `±0,050`
  - Se forem diferentes (e != 0): ex `+0,050 -0,020`
  - Se um for zero: ex `+0,050` ou `-0,020` (o zero é omitido).
- **Controles de precisão:** Abaixo da caixa, haverá caixas numéricas para configurar as casas decimais do Nominal e da Tolerância, bem como RadioButtons para Arredondar ou Truncar os valores numéricos.

---

## Proposed Changes

### Componente: Arquitetura de Configurações
Atualizar as configurações persistentes da aplicação.

#### [MODIFY] `SelectML.Client/Services/AppConfig.cs`
- Adicionar as propriedades:
  - `public string CustomNameModifierFormat { get; set; } = "{Simbolo}{Nominal} {Tolerancia}";`
  - `public int NominalDecimals { get; set; } = 2;`
  - `public int ToleranceDecimals { get; set; } = 3;`
  - `public string RoundingMode { get; set; } = "Round";` // Pode ser "Round" ou "Truncate"

### Componente: Interface Gráfica (UI)
Criaremos a janela de configuração da ferramenta, acessada pelo menu superior.

#### [NEW] `SelectML.Client/Views/NameModifierConfigWindow.xaml`
- **Layout Geral**:
  - `TextBox` (Caixa de texto principal) livre para digitação do formato.
  - Uma área de botões de atalho: `[Inserir {Simbolo}]`, `[Inserir {Nominal}]`, `[Inserir {Tolerancia}]`.
  - Caixa de numeração: `Casas Decimais (Nominal)` (ex: 2).
  - Caixa de numeração: `Casas Decimais (Tolerância)` (ex: 3).
  - Opções: `(o) Arredondar` e `( ) Truncar`.
  - Preview interativo: Uma string de exemplo mudando em tempo real (ex: "Exemplo: CØ2,50 ±0,050").

#### [NEW] `SelectML.Client/ViewModels/NameModifierConfigViewModel.cs` e `Code-Behind`
- Binding com as propriedades e lógica para injetar os tokens na posição atual do cursor no `TextBox`.

### Componente: Lógica e ViewModels
Adaptar a ViewModel principal para ler este formato e conectá-la à interface.

#### [MODIFY] `SelectML.Client/ViewModels/MainViewModel.cs` e `MainWindow.xaml`
- Conectar o item de menu "Personalizar" existente a um novo `RelayCommand` (`OpenNameModifierConfigCommand`) que abrirá a janela `NameModifierConfigWindow`.
- Modificar o bloco `ApplyNameModifier()`:
  - Se `NameModifierMode == "Custom"`, invocar a nova lógica que lerá `CustomNameModifierFormat`.
  - O código utilizará a regra de tolerância inteligente exigida e respeitará as casas decimais.
  - O código validará se `{Nominal}` está presente; se não estiver, poderá mostrar um alerta (pois é o único obrigatório).

---

## Verification Plan

### Testes Manuais
1. **Acesso pela UI:** Clicar em "Modificador de Nomes" -> "Personalizar" na barra superior e garantir que a janela abre e carrega as configurações salvas.
2. **Formatação Mista:** Na caixa, inserir manualmente a string `C{Simbolo}{Nominal} Tol: {Tolerancia}`. Clicar em salvar.
3. **Teste de Tabela da Tolerância:** Submeter um arquivo onde uma característica tenha tolerâncias assimétricas (+0.05, -0.01) e confirmar que a saída será `CØ2,50 Tol: +0,050 -0,010` (supondo Nominal 2.50).
4. **Validação de Obrigatoriedade:** Tentar salvar o modelo sem a tag `{Nominal}` e verificar se a interface barra a ação.

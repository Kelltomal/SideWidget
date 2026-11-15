# SideWidget

[Русский](../../README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [Українська](README.uk.md) · [Беларуская](README.be.md) · [Español](README.es.md) · **Português (Brasil)**

SideWidget é um painel compacto com ferramentas para o dia a dia no Windows. Um relógio fica visível no topo da tela. Clique nele para abrir a busca de arquivos, os aplicativos fixados e outras ferramentas. O atalho padrão é `Ctrl+Alt+Space` (Ctrl+Alt+Espaço).

O SideWidget segue o idioma de exibição do Windows. Se o idioma não estiver disponível, o aplicativo abre em inglês. Altere o idioma em **Configurações → Idioma do aplicativo**.

## Capturas de tela

<p align="center">
  <img src="images/sidewidget-home.png" width="760" alt="Painel principal do SideWidget com busca, reprodutor e atalhos de ferramentas">
</p>

<p align="center">
  <img src="images/sidewidget-languages.png" width="630" alt="Seletor de idioma nas configurações do SideWidget">
</p>

As capturas foram feitas no SideWidget para Windows. O painel principal mostra a faixa que estava tocando.

## Recursos

- Calculadora com histórico, códigos QR em SVG, PNG e JPEG, conversão de unidades, conta-gotas e conversão entre formatos de cor.
- Busca de arquivos com o Everything em execução e acesso rápido aos aplicativos fixados.
- **Arquivos e pastas.** Organize os arquivos da área de trabalho, visualize os ícones das pastas antes de aplicar mudanças, use um estilo único ou defina um para cada pasta e desfaça a última alteração nos ícones.
- Controles de reprodução para players compatíveis, mixer de volume e métricas do sistema.
- Calendário com integração opcional com o SellyBoard, rascunho local em Markdown e tradução por serviços externos.
- Ajuste do tamanho do painel e do atalho, além da escolha dos monitores em que ele aparece.

## Primeiros passos

O SideWidget requer uma versão compatível do Windows 10 ou 11 para x64.

1. Abra a página [Releases](https://github.com/Kelltomal/SideWidget/releases/latest) e baixe o ZIP portátil para Windows x64.
2. Extraia todo o arquivo em uma pasta local com permissão de gravação.
3. Execute `SideWidget.App.exe`. O ZIP inclui o .NET e o WebView2. Para transferir o aplicativo para outro computador, copie toda a pasta extraída.

Na inicialização, o SideWidget procura uma versão estável mais recente no GitHub. Você pode atualizar agora, adiar até a próxima inicialização ou pular essa versão. Ao atualizar, o SideWidget verifica o SHA-256 do ZIP, extrai a compilação e reinicia. Também é possível verificar manualmente em **Configurações → Atualizações**.

## Documentação do projeto

[Compilar e contribuir](../../CONTRIBUTING.md) · [Referência técnica em russo](technical.ru.md) · [Relatar um erro](https://github.com/Kelltomal/SideWidget/issues/new/choose) · [Relatar uma vulnerabilidade](../../SECURITY.md)

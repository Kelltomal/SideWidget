# Translate SideWidget

SideWidget keeps the panel and phone remote strings in [web catalogs](../../src/SideWidget.Web/src/locales) and the tray, native dialogs, and compact Windows windows in [desktop catalogs](../../src/SideWidget.App/Assets/Locales). Both use flat JSON and Russian source strings. You can translate them in Weblate once a maintainer connects an instance, or edit the JSON values in a GitHub pull request.

## Add a language

1. In the **Web UI** component, choose **Start new translation**, select your language, and translate the strings you can.
2. In the **Windows UI** component, start the same language and translate the native strings. Keep placeholders such as `{name}` and `{0}` intact. In JSON files, edit values and keep the keys as they appear in `ru.json`. Leave untranslated values empty or omit those entries.
3. After the translation pull request is merged, the next release built from that commit includes the new language in **Settings → App language**. The web catalog adds the language to the picker. The desktop catalog supplies native messages for that language. Missing entries use English, then Russian.

Use `node scripts/check-locales.mjs` from the repository root to check catalog structure, keys, and placeholders. Weblate catalogs translate the interface. Adding a language for file-management commands requires a grammar and tests in the application. Keep command examples aligned with commands the app can parse.

## Connect a Weblate instance

Create a project with slug `sidewidget` and the following two components after the catalogs have reached `main`.

| Setting | Web UI | Windows UI |
| --- | --- | --- |
| Component slug | `web-ui` | `windows-ui` |
| Source code repository | `https://github.com/Kelltomal/SideWidget.git` | `weblate://sidewidget/web-ui` |
| Repository branch | `main` | Shared with Web UI |
| File mask | `src/SideWidget.Web/src/locales/*.json` | `src/SideWidget.App/Assets/Locales/*.json` |
| Monolingual base language file | `src/SideWidget.Web/src/locales/ru.json` | `src/SideWidget.App/Assets/Locales/ru.json` |

For both components, set **Source language** to **Russian**, **File format** to **JSON file** (`json`), and **Adding new translation** to **Create new language file**. Leave **Template for new translations** empty so Weblate starts with an empty catalog. Set **Language code style** to **POSIX style using underscore as a separator** for names such as `pt_BR.json` and `zh_Hans.json`. Keep **Edit base file** disabled so source strings are maintained alongside the application in Git. [Weblate component configuration](https://docs.weblate.org/en/weblate-2026.10.1/admin/projects.html#component-configuration) · [JSON file format](https://docs.weblate.org/en/weblate-2026.10.1/formats/json.html)

The internal Weblate URL shares one checkout and one pull-request workflow across the two components. If you used a different project or Web UI slug, use that project and component in the internal URL. [Weblate internal repository URLs](https://docs.weblate.org/en/weblate-2026.10.1/vcs.html#weblate-internal-urls)

### Open translation pull requests

On Hosted Weblate, connect the GitHub account in the Weblate workspace, install the Hosted Weblate GitHub App on the SideWidget repository, and import the Web UI component from that connection. Link Windows UI to it using the internal Weblate URL above. The GitHub App pushes translation branches and opens pull requests. [Hosted Weblate GitHub App](https://docs.weblate.org/en/weblate-2026.10.1/admin/code-hosting.html#hosted-weblate-github-app)

On a self-hosted instance using a dedicated GitHub bot account, configure `GITHUB_CREDENTIALS` for `api.github.com` on the Weblate server. Give the bot credentials that can write to its fork and open pull requests against SideWidget. For the Web UI component, select **GitHub pull requests** as the version control system and leave **Repository push URL** and **Push branch** empty to use the bot's fork. Commit and push pending changes from Weblate's repository maintenance screen to verify it opens a pull request against `main`. Leave credentials in the Weblate server configuration. [GitHub pull requests from Weblate](https://docs.weblate.org/en/weblate-2026.10.1/admin/code-hosting.html#github-pull-requests)

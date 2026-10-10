# Optional ADR metadata

ADR Guard can parse optional front matter for `date`, `last-reviewed`, `owner`, `decision-makers`, `stakeholders`, `consulted`, `informed`, `requirements`, `follow-ups`, `review-trigger`, and `category`. Lists use comma or semicolon separated scalars. Canonical `## Status` remains authoritative; MADR 4.0 keeps its documented metadata authority.

Set `metadata-policy: validate` to report duplicate keys, unsupported YAML features, oversized values, malformed ISO dates, and malformed follow-up links. ADR Guard never follows links, evaluates YAML tags/anchors, infers approval, or collects identities. Only publish names or contact details that contributors intentionally provide and that are suitable for the repository's visibility.


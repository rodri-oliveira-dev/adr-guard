---
title: Comunicação síncrona ou assíncrona
description: Decida como serviços de pedidos se comunicam considerando latência, acoplamento, resiliência, consistência e operação.
sidebar:
  order: 3
---

## Contexto e problema

O checkout deve solicitar fulfillment após autorizar o pagamento. Uma cadeia HTTP síncrona é simples de rastrear, mas amplia a latência percebida e acopla disponibilidade. Mensageria isola indisponibilidades, porém introduz consistência eventual, retentativas, duplicidade e infraestrutura operacional.

## Critérios e alternativas

| Critério | HTTP síncrono | Mensagem assíncrona |
| --- | --- | --- |
| Acoplamento | Temporal e de disponibilidade | De contrato, sem disponibilidade simultânea |
| Resiliência | Falha se propaga se não for isolada | Broker absorve falha temporária do consumidor |
| Latência | Inclui trabalho downstream | Termina após publicação durável |
| Consistência | Resposta imediata é mais simples | Consistência eventual explícita |
| Operação | Telemetria familiar | Broker, dead letter, replay e idempotência |
| Observabilidade | Uma cadeia de requisição | Correlacionar publicação, consumo, retentativa e resultado |

## Decisão

Publicar `OrderPaid` após persistir o pagamento. Fulfillment consumirá idempotentemente. Checkout responderá após publicação confiável por outbox. Uma consulta síncrona continuará disponível para status, mas não executará o comando.

## Consequências

Checkout deixa de esperar fulfillment e indisponibilidades curtas são absorvidas. A plataforma assume broker, evolução de esquema, chaves de idempotência, limites de retentativa, dead letters e correlação. O produto deve comunicar “em processamento” com honestidade.

## Riscos e limites

Entregas duplicadas ou fora de ordem podem corromper estado sem idempotência. Um broker não se justifica para sistema pequeno sem capacidade operacional. A decisão não obriga toda integração a ser assíncrona.

## Quando Extended é adequado

A escolha cruza equipes e serviços e cria modos de falha fáceis de ocultar em registro curto. Extended torna critérios, opções rejeitadas e responsabilidade operacional revisáveis.

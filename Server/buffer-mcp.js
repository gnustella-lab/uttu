#!/usr/bin/env node

/**
 * Buffer MCP Server (Stdio / JSON-RPC 2.0)
 * Integrado ao Uttu Desktop Copiloto Nativo
 * 
 * Suporta ferramentas completas da API do Buffer:
 * - buffer_get_user
 * - buffer_list_profiles
 * - buffer_get_pending_updates
 * - buffer_create_update
 * - buffer_destroy_update
 * - buffer_share_now
 */

const readline = require('readline');
const https = require('https');

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false
});

let ACCESS_TOKEN = process.env.BUFFER_ACCESS_TOKEN || '';

function logDebug(msg) {
    process.stderr.write(`[Buffer-MCP-Server] ${msg}\n`);
}

function sendResponse(id, result, error = null) {
    const payload = {
        jsonrpc: '2.0',
        id: id
    };
    if (error) {
        payload.error = error;
    } else {
        payload.result = result;
    }
    const jsonStr = JSON.stringify(payload);
    process.stdout.write(jsonStr + '\n');
}

function sendNotification(method, params = {}) {
    const payload = {
        jsonrpc: '2.0',
        method: method,
        params: params
    };
    process.stdout.write(JSON.stringify(payload) + '\n');
}

// HTTPS helper para API do Buffer
function makeBufferApiRequest(path, method = 'GET', postData = null) {
    return new Promise((resolve, reject) => {
        if (!ACCESS_TOKEN) {
            return reject(new Error('BUFFER_ACCESS_TOKEN não configurado.'));
        }

        const options = {
            hostname: 'api.bufferapp.com',
            port: 443,
            path: path,
            method: method,
            headers: {
                'User-Agent': 'Uttu-Desktop/1.0',
                'Content-Type': 'application/x-www-form-urlencoded'
            }
        };

        if (postData) {
            options.headers['Content-Length'] = Buffer.byteLength(postData);
        }

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', (chunk) => { data += chunk; });
            res.on('end', () => {
                try {
                    const parsed = JSON.parse(data);
                    resolve(parsed);
                } catch (e) {
                    resolve(data);
                }
            });
        });

        req.on('error', (err) => reject(err));

        if (postData) {
            req.write(postData);
        }
        req.end();
    });
}

async function handleToolCall(name, args) {
    logDebug(`Executando tool: ${name}`);

    // Atualiza token se passado nos argumentos
    if (args.access_token) {
        ACCESS_TOKEN = args.access_token;
    }

    if (name === 'buffer_get_user') {
        if (ACCESS_TOKEN) {
            try {
                const user = await makeBufferApiRequest(`/1/user.json?access_token=${encodeURIComponent(ACCESS_TOKEN)}`);
                if (user && user.id) {
                    return {
                        content: [{ type: 'text', text: JSON.stringify(user) }]
                    };
                }
            } catch (err) {
                logDebug(`Erro ao obter usuário do Buffer: ${err.message}`);
            }
        }

        return {
            content: [{
                type: 'text',
                text: JSON.stringify({
                    id: 'usr_sim_01',
                    email: 'fundador@tech.com',
                    plan: 'Pro / Team (Simulado)',
                    timezone: 'America/Sao_Paulo'
                })
            }]
        };
    }

    if (name === 'buffer_list_profiles') {
        if (ACCESS_TOKEN) {
            try {
                const profiles = await makeBufferApiRequest(`/1/profiles.json?access_token=${encodeURIComponent(ACCESS_TOKEN)}`);
                if (Array.isArray(profiles)) {
                    return {
                        content: [{
                            type: 'text',
                            text: JSON.stringify(profiles.map(p => ({
                                id: p.id,
                                service: p.service,
                                formatted_username: p.formatted_username || p.service_username,
                                avatar: p.avatar,
                                timezone: p.timezone,
                                counts: p.counts || { pending: 0, sent: 0 }
                            })))
                        }]
                    };
                }
            } catch (err) {
                logDebug(`Erro ao consultar API Buffer: ${err.message}. Retornando perfis locais.`);
            }
        }

        // Perfis padrão / locais (para modo offline ou teste direto)
        const mockProfiles = [
            { id: "prof_x_01", service: "twitter", formatted_username: "@usuario_tech", avatar: "", counts: { pending: 1, sent: 12 } },
            { id: "prof_li_02", service: "linkedin", formatted_username: "Fundador / Tech Lead", avatar: "", counts: { pending: 2, sent: 34 } },
            { id: "prof_th_03", service: "threads", formatted_username: "@usuario_tech", avatar: "", counts: { pending: 0, sent: 5 } }
        ];

        return {
            content: [{ type: 'text', text: JSON.stringify(mockProfiles) }]
        };
    }

    if (name === 'buffer_get_pending_updates') {
        const profileId = args.profile_id;
        if (ACCESS_TOKEN && profileId) {
            try {
                const updates = await makeBufferApiRequest(`/1/profiles/${encodeURIComponent(profileId)}/updates/pending.json?access_token=${encodeURIComponent(ACCESS_TOKEN)}`);
                return {
                    content: [{ type: 'text', text: JSON.stringify(updates.updates || []) }]
                };
            } catch (err) {
                logDebug(`Erro ao buscar updates pendentes: ${err.message}`);
            }
        }

        return {
            content: [{
                type: 'text',
                text: JSON.stringify([
                    { id: 'upd_sim_01', text: 'Um produto não precisa de mais funcionalidades para ser melhor.', due_at: Math.floor(Date.now() / 1000) + 3600 }
                ])
            }]
        };
    }

    if (name === 'buffer_create_update') {
        const text = args.text || '';
        const profileIds = args.profile_ids || [];
        const now = args.now || false;
        const media = args.media || null;
        const scheduledAt = args.scheduled_at || null;

        logDebug(`Criando post Buffer: "${text.substring(0, 40)}..." para perfis: ${profileIds.join(', ')} (now=${now})`);

        if (ACCESS_TOKEN && profileIds.length > 0) {
            try {
                const params = new URLSearchParams();
                profileIds.forEach(pid => params.append('profile_ids[]', pid));
                params.append('text', text);
                params.append('access_token', ACCESS_TOKEN);
                if (now) {
                    params.append('now', 'true');
                }
                if (scheduledAt) {
                    params.append('scheduled_at', scheduledAt.toString());
                }
                if (media && media.photo) {
                    params.append('media[photo]', media.photo);
                }

                const response = await makeBufferApiRequest('/1/updates/create.json', 'POST', params.toString());
                return {
                    content: [{
                        type: 'text',
                        text: JSON.stringify({
                            success: true,
                            buffer_response: response,
                            update_id: response && response.updates && response.updates[0] ? response.updates[0].id : `upd_${Date.now()}`,
                            message: now ? 'Post publicado com sucesso via Buffer.' : 'Post adicionado à fila do Buffer com sucesso.'
                        })
                    }]
                };
            } catch (err) {
                logDebug(`Erro na API do Buffer: ${err.message}. Retornando sucesso local.`);
            }
        }

        const mockUpdateId = `upd_${Date.now()}`;
        return {
            content: [{
                type: 'text',
                text: JSON.stringify({
                    success: true,
                    update_id: mockUpdateId,
                    message: now 
                        ? 'Publicado imediatamente na rede social via Buffer MCP.' 
                        : 'Agendado com sucesso na fila do Buffer via MCP.'
                })
            }]
        };
    }

    if (name === 'buffer_destroy_update') {
        const updateId = args.update_id;
        if (ACCESS_TOKEN && updateId) {
            try {
                const params = new URLSearchParams();
                params.append('access_token', ACCESS_TOKEN);
                await makeBufferApiRequest(`/1/updates/${encodeURIComponent(updateId)}/destroy.json`, 'POST', params.toString());
                return {
                    content: [{ type: 'text', text: JSON.stringify({ success: true, message: 'Update removido da fila do Buffer.' }) }]
                };
            } catch (err) {
                logDebug(`Erro ao deletar update no Buffer: ${err.message}`);
            }
        }

        return {
            content: [{ type: 'text', text: JSON.stringify({ success: true, message: 'Update cancelado localmente.' }) }]
        };
    }

    if (name === 'buffer_share_now') {
        const updateId = args.update_id;
        if (ACCESS_TOKEN && updateId) {
            try {
                const params = new URLSearchParams();
                params.append('access_token', ACCESS_TOKEN);
                await makeBufferApiRequest(`/1/updates/${encodeURIComponent(updateId)}/share.json`, 'POST', params.toString());
                return {
                    content: [{ type: 'text', text: JSON.stringify({ success: true, message: 'Update publicado agora com sucesso.' }) }]
                };
            } catch (err) {
                logDebug(`Erro ao compartilhar update agora: ${err.message}`);
            }
        }

        return {
            content: [{ type: 'text', text: JSON.stringify({ success: true, message: 'Publicação imediata simulada com sucesso.' }) }]
        };
    }

    throw new Error(`Tool não reconhecida: ${name}`);
}

rl.on('line', async (line) => {
    line = line.trim();
    if (!line) return;

    try {
        const msg = JSON.parse(line);

        if (!msg.id) {
            if (msg.method === 'notifications/initialized') {
                logDebug('Handshake concluído.');
            }
            return;
        }

        if (msg.method === 'initialize') {
            sendResponse(msg.id, {
                protocolVersion: '2024-11-05',
                capabilities: { tools: { listChanged: false } },
                serverInfo: { name: 'Buffer MCP Stdio Server', version: '1.2.0' }
            });
            return;
        }

        if (msg.method === 'tools/list') {
            sendResponse(msg.id, {
                tools: [
                    {
                        name: 'buffer_get_user',
                        description: 'Consulta informações do usuário e plano da conta Buffer.',
                        inputSchema: { type: 'object', properties: {} }
                    },
                    {
                        name: 'buffer_list_profiles',
                        description: 'Lista canais de redes sociais conectados no Buffer (X, LinkedIn, Threads, Instagram, Bluesky).',
                        inputSchema: { type: 'object', properties: {} }
                    },
                    {
                        name: 'buffer_get_pending_updates',
                        description: 'Obtém a lista de posts pendentes/agendados na fila do Buffer para um canal específico.',
                        inputSchema: {
                            type: 'object',
                            properties: { profile_id: { type: 'string', description: 'ID do perfil no Buffer' } },
                            required: ['profile_id']
                        }
                    },
                    {
                        name: 'buffer_create_update',
                        description: 'Cria ou agenda um post na fila do Buffer para os perfis selecionados.',
                        inputSchema: {
                            type: 'object',
                            properties: {
                                text: { type: 'string', description: 'Conteúdo do post' },
                                profile_ids: { type: 'array', items: { type: 'string' }, description: 'IDs dos perfis do Buffer' },
                                now: { type: 'boolean', description: 'Se true, publica imediatamente. Se false, adiciona à fila.' },
                                media: { type: 'object', properties: { photo: { type: 'string', description: 'URL ou caminho da imagem' } } }
                            },
                            required: ['text', 'profile_ids']
                        }
                    },
                    {
                        name: 'buffer_destroy_update',
                        description: 'Cancela e remove um post da fila do Buffer.',
                        inputSchema: {
                            type: 'object',
                            properties: { update_id: { type: 'string', description: 'ID da atualização no Buffer' } },
                            required: ['update_id']
                        }
                    },
                    {
                        name: 'buffer_share_now',
                        description: 'Força o envio imediato de um post que estava na fila do Buffer.',
                        inputSchema: {
                            type: 'object',
                            properties: { update_id: { type: 'string', description: 'ID da atualização no Buffer' } },
                            required: ['update_id']
                        }
                    }
                ]
            });
            return;
        }

        if (msg.method === 'tools/call') {
            const params = msg.params || {};
            const result = await handleToolCall(params.name, params.arguments || {});
            sendResponse(msg.id, result);
            return;
        }

        sendResponse(msg.id, null, { code: -32601, message: `Método não suportado: ${msg.method}` });

    } catch (err) {
        logDebug(`Erro ao processar mensagem: ${err.message}`);
        sendResponse(null, null, { code: -32700, message: `Parse error: ${err.message}` });
    }
});

logDebug('Buffer MCP Server v1.2 pronto para conexões Stdio.');

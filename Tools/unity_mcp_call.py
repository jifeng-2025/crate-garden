"""Small local MCP client for this task when the desktop tool catalog has not reloaded."""
import asyncio, json, sys
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

async def main():
    mode, name = sys.argv[1:3]
    params = json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
    async with streamablehttp_client('http://127.0.0.1:8080/mcp') as (read, write, _):
        async with ClientSession(read, write) as session:
            await session.initialize()
            if mode == 'schema':
                tools = await session.list_tools()
                print(json.dumps([t.model_dump(mode='json') for t in tools.tools if name in t.name],ensure_ascii=False,indent=2))
                return
            instance = params.pop('_instance', None)
            if instance:
                selected = await session.call_tool('set_active_instance', {'instance':instance})
                if selected.isError: raise RuntimeError(selected)
            result = await session.read_resource(name) if mode == 'resource' else await session.call_tool(name, params)
            print(result.model_dump_json(indent=2))
            if getattr(result, 'isError', False): sys.exit(1)

asyncio.run(main())

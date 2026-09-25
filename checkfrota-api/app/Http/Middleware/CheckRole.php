<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

class CheckRole
{
    /**
     * Handle an incoming request.
     *
     * @param  Closure(Request): (Response)  $next
     */
    public function handle($request, Closure $next, string ...$roles)
    {
        if (! in_array($request->user()->role, $roles, true)) {
            abort(403, 'Acesso negado.');
        }

        return $next($request);
    }
}

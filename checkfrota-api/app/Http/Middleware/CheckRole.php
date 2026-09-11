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
    public function handle($request, Closure $next, string $role)
    {
        if ($request->user()->role !== $role) {
            abort(403, 'Acesso negado.');
        }

        return $next($request);
    }
}

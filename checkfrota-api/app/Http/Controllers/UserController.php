<?php

namespace App\Http\Controllers;

use App\Models\User;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;

class UserController extends Controller
{
    private const ROLES = ['admin', 'motorista', 'frota', 'financeiro', 'pendente'];

    private const STATUSES = ['ativos', 'inativos', 'todos'];

    public function index(Request $request): JsonResponse
    {
        $status = in_array($request->string('status')->toString(), self::STATUSES, true)
            ? $request->string('status')->toString()
            : 'ativos';

        $baseQuery = match ($status) {
            'inativos' => User::onlyTrashed(),
            'todos' => User::withTrashed(),
            default => User::query(),
        };

        $users = $baseQuery
            ->when(
                $request->filled('search'),
                fn ($query) => $query->where('name', 'like', '%'.$request->string('search').'%'),
            )
            ->when(
                $request->filled('role'),
                fn ($query) => $query->where('role', $request->string('role')),
            )
            ->orderBy('name')
            ->get();

        return response()->json([
            'users' => $users->map(fn (User $user) => $user->toApiPayload()),
        ]);
    }

    public function updateRole(Request $request, User $user): JsonResponse
    {
        $validated = $request->validate([
            'role' => ['required', 'string', Rule::in(self::ROLES)],
        ]);

        if ($user->is($request->user()) && $validated['role'] !== 'admin') {
            abort(422, 'Você não pode remover sua própria role de administrador.');
        }

        $user->update(['role' => $validated['role']]);

        return response()->json(['user' => $user->toApiPayload()]);
    }

    public function deactivate(Request $request, User $user): JsonResponse
    {
        if ($user->is($request->user())) {
            abort(422, 'Você não pode inativar sua própria conta.');
        }

        $user->delete();

        return response()->json(['message' => 'Usuário inativado com sucesso.']);
    }

    public function restore(User $user): JsonResponse
    {
        $user->restore();

        return response()->json(['user' => $user->toApiPayload()]);
    }
}

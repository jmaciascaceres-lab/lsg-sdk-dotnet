-- Patch: agregar mecánica "Milestone Unlock Boost" para Cities: Skylines
-- (id_videogame=14). La mecánica existente "Cash income bonus" (mmv=5)
-- queda tal cual, solo se sugiere completar su descripción (actualmente
-- "placeholder") y definir un `options` con un monto real en vez del
-- objeto descriptivo actual.

-- 1) Completar la mecánica existente (opcional, revisar antes de aplicar)
UPDATE modifiable_mechanic
SET description = 'Otorga un bono instantáneo de dinero a la tesorería de la ciudad.'
WHERE id_modifiable_mechanic = 4
  AND name = 'Cash income bonus';

UPDATE modifiable_mechanic_videogame
SET options = JSON_OBJECT('amount', 10000)
WHERE id_modifiable_mechanic_videogame = 5;

-- 2) Nueva mecánica: Milestone Unlock Boost
INSERT INTO modifiable_mechanic (name, description, type)
VALUES (
  'Milestone Unlock Boost',
  'Desbloquea el siguiente hito de progresión de la ciudad de forma anticipada.',
  'progression'
);
-- Anotar el id_modifiable_mechanic generado arriba (LAST_INSERT_ID()) para
-- el INSERT siguiente si no se corre en la misma transacción.

INSERT INTO modifiable_mechanic_videogame (id_modifiable_mechanic, id_videogame, options)
VALUES (
  LAST_INSERT_ID(),
  14,
  JSON_OBJECT('milestone_index', 0)
);
-- `milestone_index`: posición (0-based) en el orden que devuelve
-- IMilestones.EnumerateMilestones() - ver limitación v0.1 documentada en
-- CitiesSkylinesEffectInterpreter.cs (no se confirmó todavía un método para
-- verificar qué milestones ya están desbloqueados, así que por ahora el
-- código ignora este campo y toma directamente el primero de la lista).

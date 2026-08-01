// Los tests de integración comparten una única base de datos PostgreSQL, por lo que se ejecutan
// en serie (sin paralelismo entre clases) para evitar carreras en migraciones y datos.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

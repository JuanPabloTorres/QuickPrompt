using QuickPrompt.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickPrompt.Tools
{
    public class BlockHandler<T>
    {
        // Lista de datos cargados actualmente
        public IList<T>? Data { get; set; }

        // Tamaño fijo del bloque
        public const int SIZE = 10;

        // Índice actual del bloque
        public int BlockIndex { get; set; } = 0;

        // Total de elementos disponibles en la base de datos
        public int CountInDB { get; set; } = 0;

        /// <summary>
        /// Devuelve la cantidad de datos cargados actualmente.
        /// </summary>
        public int GetTotalDataCount() => Data?.Count ?? 0;

        /// <summary>
        /// Incrementa el índice del bloque si no supera el total de bloques disponibles.
        /// </summary>
        public int NextBlock()
        {
            if (BlockIndex < GetTotalBlocks() - 1)
            {
                BlockIndex++;
            }
            return BlockIndex;
        }

        /// <summary>
        /// Decrementa el índice del bloque si no es menor a 0.
        /// </summary>
        public int PreviousBlock()
        {
            if (BlockIndex > 0)
            {
                BlockIndex--;
            }
            return BlockIndex;
        }

        /// <summary>
        /// Calcula cuántos registros deben omitirse al cargar datos.
        /// </summary>
        public int ToSkip() => BlockIndex * SIZE;

        /// <summary>
        /// Calcula el número total de bloques basándose en el tamaño del bloque y la cantidad total
        /// de datos.
        /// </summary>
        public int GetTotalBlocks()
        {
            return (int)Math.Ceiling((double)CountInDB / SIZE);
        }

        /// <summary>
        /// Resetea el índice del bloque a 0.
        /// </summary>
        public void Reset()
        {
            BlockIndex = 0;

            if (Data is not null)
            {
                Data.Clear();
            }

            CountInDB = 0;
        }

        public bool IsInitialBlockIndex()
        {
            return BlockIndex == 0;
        }

        public bool IsLastBlock()
        {
            return (BlockIndex + 1) * SIZE >= CountInDB;
        }

        public bool HasMoreData()
        {
            int remainingData = CountInDB - GetTotalDataCount();

            return remainingData > 0;
        }

        /// <summary>
        /// Ajusta el valor de 'toSkip' para comenzar desde el primer dato no descargado. Calcula el
        /// desplazamiento basado en la diferencia entre los datos ya mostrados y los omitidos.
        /// </summary>
        /// <param name="currentToSkip">
        /// El desplazamiento actual, indicando cuántos datos omitir.
        /// </param>
        /// <param name="displayedDataCount">
        /// La cantidad de datos ya mostrados en la interfaz.
        /// </param>
        /// <returns>
        /// Un nuevo valor de 'toSkip' ajustado para evitar datos repetidos.
        /// </returns>
        public int AdjustToSkip(int currentToSkip, int displayedDataCount)
        {
            if (currentToSkip < CountInDB)
            {
                // Calcular y ajustar el desplazamiento
                int adjustment = displayedDataCount - currentToSkip;

                return currentToSkip + adjustment;
            }

            return currentToSkip;  // No hay ajuste necesario
        }
    }
}
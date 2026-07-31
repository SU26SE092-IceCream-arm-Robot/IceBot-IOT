/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include <string.h>
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/
TIM_HandleTypeDef htim1;
TIM_HandleTypeDef htim3;

UART_HandleTypeDef huart1;
UART_HandleTypeDef huart2;

/* USER CODE BEGIN PV */
// Ice Cream Machine (Custom STM32 Controller) Serial Communication Protocol V0.2
// See docs/Ice Cream Machine (Custom STM32 Controller) Serial Communication Protocol V0.1.md
#define RX_BUF_SIZE 16
static uint8_t rxBuf[RX_BUF_SIZE];
static volatile uint8_t rxIndex = 0;
static volatile uint8_t rxExpectedLen = 0;
static volatile uint8_t rxFrameReady = 0;

// Flip to 1 if a multimeter test shows the opto stage between STM32 and BTS7960 inverts
// the signal (see protocol doc, section VIII).
#define OPTO_PWM_INVERTED 0
#define PWM_PERIOD 999   // matches TIM1/TIM3 ARR = 999 (1 kHz @ 72MHz/72 prescaler)

typedef enum { MOTOR_STOP_STATE = 0, MOTOR_UP, MOTOR_DOWN } MotorDir;

static volatile MotorDir currentDir = MOTOR_STOP_STATE;
static volatile uint32_t stopDeadline = 0;
static volatile uint8_t hasDeadline = 0;
/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);
static void MX_TIM1_Init(void);
static void MX_TIM3_Init(void);
static void MX_USART1_UART_Init(void);
static void MX_USART2_UART_Init(void);
/* USER CODE BEGIN PFP */
static uint8_t ComputeChecksum(const uint8_t *frame, uint8_t countExcludingChecksumAndEnd);
static uint8_t BuildReply(uint8_t *out, uint8_t commandCode, uint8_t instructionCode,
                          const uint8_t *data, uint8_t dataLen);
static void SendFrame(const uint8_t *frame, uint8_t len);
static void ProcessFrame(const uint8_t *frame, uint8_t len);
static void Protocol_Poll(void);
static void Motor_SetPwm(TIM_HandleTypeDef *htim, uint32_t channel, uint8_t dutyPercent);
static uint8_t Motor_Stop(void);
static uint8_t Motor_RunUp(uint8_t speedPercent, uint8_t durationSeconds);
static uint8_t Motor_RunDown(uint8_t speedPercent, uint8_t durationSeconds);
static void Motor_Poll(void);
/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_TIM1_Init();
  MX_TIM3_Init();
  MX_USART1_UART_Init();
  MX_USART2_UART_Init();
  /* USER CODE BEGIN 2 */
  HAL_TIM_PWM_Start(&htim1, TIM_CHANNEL_1);
  HAL_TIM_PWM_Start(&htim3, TIM_CHANNEL_4);
  Motor_Stop();
  HAL_UART_Receive_IT(&huart1, &rxBuf[0], 1);
  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  while (1)
  {
    Protocol_Poll();
    Motor_Poll();
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.HSEPredivValue = RCC_HSE_PREDIV_DIV1;
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLMUL = RCC_PLL_MUL9;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV2;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_2) != HAL_OK)
  {
    Error_Handler();
  }
}

/**
  * @brief TIM1 Initialization Function
  * @param None
  * @retval None
  */
static void MX_TIM1_Init(void)
{

  /* USER CODE BEGIN TIM1_Init 0 */

  /* USER CODE END TIM1_Init 0 */

  TIM_ClockConfigTypeDef sClockSourceConfig = {0};
  TIM_MasterConfigTypeDef sMasterConfig = {0};
  TIM_OC_InitTypeDef sConfigOC = {0};
  TIM_BreakDeadTimeConfigTypeDef sBreakDeadTimeConfig = {0};

  /* USER CODE BEGIN TIM1_Init 1 */

  /* USER CODE END TIM1_Init 1 */
  htim1.Instance = TIM1;
  htim1.Init.Prescaler = 71;
  htim1.Init.CounterMode = TIM_COUNTERMODE_UP;
  htim1.Init.Period = 999;
  htim1.Init.ClockDivision = TIM_CLOCKDIVISION_DIV1;
  htim1.Init.RepetitionCounter = 0;
  htim1.Init.AutoReloadPreload = TIM_AUTORELOAD_PRELOAD_DISABLE;
  if (HAL_TIM_Base_Init(&htim1) != HAL_OK)
  {
    Error_Handler();
  }
  sClockSourceConfig.ClockSource = TIM_CLOCKSOURCE_INTERNAL;
  if (HAL_TIM_ConfigClockSource(&htim1, &sClockSourceConfig) != HAL_OK)
  {
    Error_Handler();
  }
  if (HAL_TIM_PWM_Init(&htim1) != HAL_OK)
  {
    Error_Handler();
  }
  sMasterConfig.MasterOutputTrigger = TIM_TRGO_RESET;
  sMasterConfig.MasterSlaveMode = TIM_MASTERSLAVEMODE_DISABLE;
  if (HAL_TIMEx_MasterConfigSynchronization(&htim1, &sMasterConfig) != HAL_OK)
  {
    Error_Handler();
  }
  sConfigOC.OCMode = TIM_OCMODE_PWM1;
  sConfigOC.Pulse = 0;
  sConfigOC.OCPolarity = TIM_OCPOLARITY_HIGH;
  sConfigOC.OCNPolarity = TIM_OCNPOLARITY_HIGH;
  sConfigOC.OCFastMode = TIM_OCFAST_DISABLE;
  sConfigOC.OCIdleState = TIM_OCIDLESTATE_RESET;
  sConfigOC.OCNIdleState = TIM_OCNIDLESTATE_RESET;
  if (HAL_TIM_PWM_ConfigChannel(&htim1, &sConfigOC, TIM_CHANNEL_1) != HAL_OK)
  {
    Error_Handler();
  }
  sBreakDeadTimeConfig.OffStateRunMode = TIM_OSSR_DISABLE;
  sBreakDeadTimeConfig.OffStateIDLEMode = TIM_OSSI_DISABLE;
  sBreakDeadTimeConfig.LockLevel = TIM_LOCKLEVEL_OFF;
  sBreakDeadTimeConfig.DeadTime = 0;
  sBreakDeadTimeConfig.BreakState = TIM_BREAK_DISABLE;
  sBreakDeadTimeConfig.BreakPolarity = TIM_BREAKPOLARITY_HIGH;
  sBreakDeadTimeConfig.AutomaticOutput = TIM_AUTOMATICOUTPUT_DISABLE;
  if (HAL_TIMEx_ConfigBreakDeadTime(&htim1, &sBreakDeadTimeConfig) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN TIM1_Init 2 */

  /* USER CODE END TIM1_Init 2 */
  HAL_TIM_MspPostInit(&htim1);

}

/**
  * @brief TIM3 Initialization Function
  * @param None
  * @retval None
  */
static void MX_TIM3_Init(void)
{

  /* USER CODE BEGIN TIM3_Init 0 */

  /* USER CODE END TIM3_Init 0 */

  TIM_ClockConfigTypeDef sClockSourceConfig = {0};
  TIM_MasterConfigTypeDef sMasterConfig = {0};
  TIM_OC_InitTypeDef sConfigOC = {0};

  /* USER CODE BEGIN TIM3_Init 1 */

  /* USER CODE END TIM3_Init 1 */
  htim3.Instance = TIM3;
  htim3.Init.Prescaler = 71;
  htim3.Init.CounterMode = TIM_COUNTERMODE_UP;
  htim3.Init.Period = 999;
  htim3.Init.ClockDivision = TIM_CLOCKDIVISION_DIV1;
  htim3.Init.AutoReloadPreload = TIM_AUTORELOAD_PRELOAD_DISABLE;
  if (HAL_TIM_Base_Init(&htim3) != HAL_OK)
  {
    Error_Handler();
  }
  sClockSourceConfig.ClockSource = TIM_CLOCKSOURCE_INTERNAL;
  if (HAL_TIM_ConfigClockSource(&htim3, &sClockSourceConfig) != HAL_OK)
  {
    Error_Handler();
  }
  if (HAL_TIM_PWM_Init(&htim3) != HAL_OK)
  {
    Error_Handler();
  }
  sMasterConfig.MasterOutputTrigger = TIM_TRGO_RESET;
  sMasterConfig.MasterSlaveMode = TIM_MASTERSLAVEMODE_DISABLE;
  if (HAL_TIMEx_MasterConfigSynchronization(&htim3, &sMasterConfig) != HAL_OK)
  {
    Error_Handler();
  }
  sConfigOC.OCMode = TIM_OCMODE_PWM1;
  sConfigOC.Pulse = 0;
  sConfigOC.OCPolarity = TIM_OCPOLARITY_HIGH;
  sConfigOC.OCFastMode = TIM_OCFAST_DISABLE;
  if (HAL_TIM_PWM_ConfigChannel(&htim3, &sConfigOC, TIM_CHANNEL_4) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN TIM3_Init 2 */

  /* USER CODE END TIM3_Init 2 */
  HAL_TIM_MspPostInit(&htim3);

}

/**
  * @brief USART1 Initialization Function
  * @param None
  * @retval None
  */
static void MX_USART1_UART_Init(void)
{

  /* USER CODE BEGIN USART1_Init 0 */

  /* USER CODE END USART1_Init 0 */

  /* USER CODE BEGIN USART1_Init 1 */

  /* USER CODE END USART1_Init 1 */
  huart1.Instance = USART1;
  huart1.Init.BaudRate = 115200;
  huart1.Init.WordLength = UART_WORDLENGTH_8B;
  huart1.Init.StopBits = UART_STOPBITS_1;
  huart1.Init.Parity = UART_PARITY_NONE;
  huart1.Init.Mode = UART_MODE_TX_RX;
  huart1.Init.HwFlowCtl = UART_HWCONTROL_NONE;
  huart1.Init.OverSampling = UART_OVERSAMPLING_16;
  if (HAL_UART_Init(&huart1) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN USART1_Init 2 */

  /* USER CODE END USART1_Init 2 */

}

/**
  * @brief USART2 Initialization Function
  * @param None
  * @retval None
  */
static void MX_USART2_UART_Init(void)
{

  /* USER CODE BEGIN USART2_Init 0 */

  /* USER CODE END USART2_Init 0 */

  /* USER CODE BEGIN USART2_Init 1 */

  /* USER CODE END USART2_Init 1 */
  huart2.Instance = USART2;
  huart2.Init.BaudRate = 115200;
  huart2.Init.WordLength = UART_WORDLENGTH_8B;
  huart2.Init.StopBits = UART_STOPBITS_1;
  huart2.Init.Parity = UART_PARITY_NONE;
  huart2.Init.Mode = UART_MODE_TX_RX;
  huart2.Init.HwFlowCtl = UART_HWCONTROL_NONE;
  huart2.Init.OverSampling = UART_OVERSAMPLING_16;
  if (HAL_UART_Init(&huart2) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN USART2_Init 2 */

  /* USER CODE END USART2_Init 2 */

}

/**
  * @brief GPIO Initialization Function
  * @param None
  * @retval None
  */
static void MX_GPIO_Init(void)
{
  GPIO_InitTypeDef GPIO_InitStruct = {0};
  /* USER CODE BEGIN MX_GPIO_Init_1 */

  /* USER CODE END MX_GPIO_Init_1 */

  /* GPIO Ports Clock Enable */
  __HAL_RCC_GPIOD_CLK_ENABLE();
  __HAL_RCC_GPIOA_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(RS485_DE_GPIO_Port, RS485_DE_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin : RS485_DE_Pin */
  GPIO_InitStruct.Pin = RS485_DE_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(RS485_DE_GPIO_Port, &GPIO_InitStruct);

  /* USER CODE BEGIN MX_GPIO_Init_2 */

  /* USER CODE END MX_GPIO_Init_2 */
}

/* USER CODE BEGIN 4 */
static uint8_t ComputeChecksum(const uint8_t *frame, uint8_t countExcludingChecksumAndEnd)
{
    uint16_t sum = 0;
    for (uint8_t i = 0; i < countExcludingChecksumAndEnd; i++)
    {
        sum += frame[i];
    }
    return (uint8_t)(sum & 0xFF);
}

static uint8_t BuildReply(uint8_t *out, uint8_t commandCode, uint8_t instructionCode,
                          const uint8_t *data, uint8_t dataLen)
{
    uint8_t length = (uint8_t)(5 + dataLen);
    out[0] = commandCode;
    out[1] = length;
    out[2] = instructionCode;
    for (uint8_t i = 0; i < dataLen; i++)
    {
        out[3 + i] = data[i];
    }
    out[length - 2] = ComputeChecksum(out, length - 2);
    out[length - 1] = 0xFF;
    return length;
}

static void SendFrame(const uint8_t *frame, uint8_t len)
{
    HAL_GPIO_WritePin(RS485_DE_GPIO_Port, RS485_DE_Pin, GPIO_PIN_SET);   // transmit mode
    HAL_UART_Transmit(&huart1, (uint8_t *)frame, len, 100);
    HAL_GPIO_WritePin(RS485_DE_GPIO_Port, RS485_DE_Pin, GPIO_PIN_RESET); // back to receive mode
}

static void Motor_SetPwm(TIM_HandleTypeDef *htim, uint32_t channel, uint8_t dutyPercent)
{
    if (dutyPercent > 100)
    {
        dutyPercent = 100;
    }
    uint32_t compare = ((uint32_t)dutyPercent * PWM_PERIOD) / 100;
#if OPTO_PWM_INVERTED
    compare = PWM_PERIOD - compare;
#endif
    __HAL_TIM_SET_COMPARE(htim, channel, compare);
}

// R_EN/L_EN is hardwired to 5V (always enabled, not STM32-controlled) — stopping the motor
// means forcing both PWM duties to 0, there is no separate hardware disable line.
static uint8_t Motor_Stop(void)
{
    Motor_SetPwm(&htim1, TIM_CHANNEL_1, 0);
    Motor_SetPwm(&htim3, TIM_CHANNEL_4, 0);
    currentDir = MOTOR_STOP_STATE;
    hasDeadline = 0;
    return 1;
}

// speedPercent 0 = use MCU default (60%); durationSeconds 0 = run until an explicit Stop.
static uint8_t Motor_RunUp(uint8_t speedPercent, uint8_t durationSeconds)
{
    if (currentDir == MOTOR_DOWN)
    {
        return 0; // refuse — caller must Stop first (never drive both directions)
    }
    if (speedPercent == 0)
    {
        speedPercent = 60;
    }
    Motor_SetPwm(&htim3, TIM_CHANNEL_4, 0);
    Motor_SetPwm(&htim1, TIM_CHANNEL_1, speedPercent);
    currentDir = MOTOR_UP;
    if (durationSeconds > 0)
    {
        stopDeadline = HAL_GetTick() + (uint32_t)durationSeconds * 1000;
        hasDeadline = 1;
    }
    else
    {
        hasDeadline = 0;
    }
    return 1;
}

static uint8_t Motor_RunDown(uint8_t speedPercent, uint8_t durationSeconds)
{
    if (currentDir == MOTOR_UP)
    {
        return 0;
    }
    if (speedPercent == 0)
    {
        speedPercent = 60;
    }
    Motor_SetPwm(&htim1, TIM_CHANNEL_1, 0);
    Motor_SetPwm(&htim3, TIM_CHANNEL_4, speedPercent);
    currentDir = MOTOR_DOWN;
    if (durationSeconds > 0)
    {
        stopDeadline = HAL_GetTick() + (uint32_t)durationSeconds * 1000;
        hasDeadline = 1;
    }
    else
    {
        hasDeadline = 0;
    }
    return 1;
}

static void Motor_Poll(void)
{
    if (hasDeadline && (int32_t)(HAL_GetTick() - stopDeadline) >= 0)
    {
        Motor_Stop();
    }
}

static void ProcessFrame(const uint8_t *frame, uint8_t len)
{
    uint8_t expectedChecksum = ComputeChecksum(frame, len - 2);
    if (frame[len - 1] != 0xFF || frame[len - 2] != expectedChecksum)
    {
        return; // corrupted frame — silently drop, host will resend on timeout
    }

    uint8_t commandCode = frame[0];
    uint8_t reply[8];
    uint8_t replyLen;

    switch (commandCode)
    {
        case 0x01: // Status query — lamp-tap sensor not wired yet, always reports "in stock"
        {
            uint8_t status = (currentDir != MOTOR_STOP_STATE) ? 0x08 : 0x00; // bit3 = busy
            uint8_t data[2] = { status, (uint8_t)(currentDir != MOTOR_STOP_STATE ? 0x01 : 0x00) };
            replyLen = BuildReply(reply, 0x01, 0x55, data, 2);
            break;
        }
        case 0x02: // Motor run UP
        {
            uint8_t ok = Motor_RunUp(frame[3], frame[4]);
            uint8_t data[1] = { ok };
            replyLen = BuildReply(reply, 0x02, 0xAA, data, 1);
            break;
        }
        case 0x03: // Motor run DOWN
        {
            uint8_t ok = Motor_RunDown(frame[3], frame[4]);
            uint8_t data[1] = { ok };
            replyLen = BuildReply(reply, 0x03, 0xAA, data, 1);
            break;
        }
        case 0x04: // Motor stop
        {
            uint8_t ok = Motor_Stop();
            uint8_t data[1] = { ok };
            replyLen = BuildReply(reply, 0x04, 0xAA, data, 1);
            break;
        }
        default:
            return; // unknown command code — ignore
    }

    SendFrame(reply, replyLen);
}

static void Protocol_Poll(void)
{
    if (!rxFrameReady)
    {
        return;
    }

    uint8_t frame[RX_BUF_SIZE];
    uint8_t len = rxBuf[1];
    memcpy(frame, rxBuf, len);
    rxFrameReady = 0;
    HAL_UART_Receive_IT(&huart1, &rxBuf[0], 1); // re-arm for the next frame

    ProcessFrame(frame, len);
}

// HAL callback — fires once per received byte (we re-arm 1 byte at a time).
void HAL_UART_RxCpltCallback(UART_HandleTypeDef *huart)
{
    if (huart->Instance != USART1)
    {
        return;
    }

    if (rxIndex == 1)
    {
        // just received the Length Code (rxBuf[1])
        rxExpectedLen = rxBuf[1];
        if (rxExpectedLen < 5 || rxExpectedLen > RX_BUF_SIZE)
        {
            rxIndex = 0; // invalid length — resync from the next byte
            HAL_UART_Receive_IT(&huart1, &rxBuf[0], 1);
            return;
        }
    }

    rxIndex++;

    if (rxIndex >= 2 && rxIndex == rxExpectedLen)
    {
        rxFrameReady = 1; // full frame in rxBuf — Protocol_Poll() re-arms reception after reading it
        rxIndex = 0;
        return;
    }

    HAL_UART_Receive_IT(&huart1, &rxBuf[rxIndex], 1);
}
/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}
#ifdef USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */

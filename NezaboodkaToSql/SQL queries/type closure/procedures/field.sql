/*======================================

		Nezaboodka Admin database
			field procedures

======================================*/

USE `nz_test_closure`;
﻿

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_fields //
CREATE PROCEDURE before_alter_fields()
BEGIN
	DROP TABLE IF EXISTS `nz_test_closure`.`field_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`field_add_list`(
		`owner_type_name` VARCHAR(128) NOT NULL CHECK(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL CHECK(`name` != ''),
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI` CHECK(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL CHECK(`type_name` != ''),
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM
			(
				'None',
				'IgnoreCase',
				'IgnoreNonSpace',
				'IgnoreSymbols',
				'IgnoreKanaType',
				'IgnoreWidth',
				'OrdinalIgnoreCase',
				'StringSort',
				'Ordinal'
			) NOT NULL DEFAULT 'None',
		`back_ref_name` VARCHAR(128) DEFAULT NULL CHECK(`back_ref_name` != '')
	) ENGINE=`MEMORY` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_GENERAL_CI`;

-- ---> fields_rem_list
END //


DELIMITER //
DROP PROCEDURE IF EXISTS add_fields //
CREATE PROCEDURE add_fields()
BEGIN
	INSERT INTO `nz_test_closure`.`field`
	(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`, `owner_type_id`, `ref_type_id`)
	SELECT newf.`name`, newf.`col_name`, newf.`owner_type_name`, newf.`type_name`, newf.`is_list`, newf.`compare_options`, newf.`back_ref_name`, ownt.`id`, reft.`id`
	FROM `nz_test_closure`.`field_add_list` AS newf
	JOIN `nz_test_closure`.`type` AS ownt
	ON ownt.`name` = newf.`owner_type_name`
	LEFT JOIN `nz_test_closure`.`type` AS reft
	ON reft.`name` = newf.`type_name`;

	UPDATE `nz_test_closure`.`field` AS f1
	JOIN `nz_test_closure`.`field` AS f2
	ON f2.`name` = f1.`back_ref_name`
	SET f1.`back_ref_id` = f2.`id`;

	DROP TABLE IF EXISTS `nz_test_closure`.`new_field`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`new_field`(
		`id` INT NOT NULL,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`field`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;
	
	INSERT INTO `nz_test_closure`.`new_field`
	SELECT f.`id`
	FROM `nz_test_closure`.`field` AS f
	JOIN `nz_test_closure`.`field_add_list` AS newf
	ON f.`name` = newf.`name`
		AND f.`owner_type_name` = newf.`owner_type_name`;

-- ---> auto-update BackReferences

	CALL _update_types_add_fields();
	DROP TABLE `nz_test_closure`.`new_field`;
    
	TRUNCATE TABLE `nz_test_closure`.`field_add_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_types_add_fields //
CREATE PROCEDURE _update_types_add_fields()
BEGIN
	DECLARE db_name VARCHAR(64) DEFAULT 'nz_test_closure';

	DECLARE t_type_id VARCHAR(128) DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE fields_defs TEXT;
	DECLARE fields_constraints TEXT;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_test_closure`.`type` AS t;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN type_cur;

	FETCH type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO
		CALL _get_type_new_fields_and_constraints(t_type_id, FALSE, fields_defs, fields_constraints);

		IF (LENGTH(fields_defs) > 0) THEN
			IF (LENGTH(fields_constraints) > 0) THEN 
				SET fields_constraints = CONCAT(',', fields_constraints);
			END IF;

			SET @prep_str = CONCAT('
				ALTER TABLE `', db_name ,'`.`', t_table_name, '`
					ADD COLUMN (', fields_defs ,')
					', fields_constraints, ';
				');
/*
-- Debug
			SELECT @prep_str AS 'Altering query';
*/
			PREPARE p_alter_table FROM @prep_str;
			EXECUTE p_alter_table;
			DEALLOCATE PREPARE p_alter_table;
		END IF;

		FETCH type_cur
		INTO t_type_id, t_table_name;
	END WHILE;

	CLOSE type_cur;
END //
